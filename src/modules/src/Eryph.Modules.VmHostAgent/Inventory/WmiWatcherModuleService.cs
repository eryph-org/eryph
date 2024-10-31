using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement;
using Eryph.VmManagement.Data;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Transport;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    internal class WmiWatcherModuleService : IHostedService
    {
        private readonly IBus _bus;
        private readonly ILogger _log;
        private readonly WorkflowOptions _workflowOptions;

        private ManagementEventWatcher _networkWatcher;
        private ManagementEventWatcher _statusWatcher;
        private ManagementEventWatcher _vmWatcher;
        private readonly Timer _upTimeTimer;

        private const int UpTimeCheckSeconds= 60;

        public WmiWatcherModuleService(IBus bus, ILogger log, WorkflowOptions _workflowOptions)
        {
            _bus = bus;
            _log = log;
            this._workflowOptions = _workflowOptions;

            _upTimeTimer = new Timer(UpTimeCheck, null, Timeout.Infinite, 0);
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartWatching();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _networkWatcher?.Dispose();
            _statusWatcher?.Dispose();
            _vmWatcher?.Dispose();

            _upTimeTimer.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void StartWatching()
        {
            _log.LogDebug("Starting WMI Watcher");

            var scope = new ManagementScope(@"\root\virtualization\v2");


            _networkWatcher = new ManagementEventWatcher(
                scope,
                new WqlEventQuery(
                "Select * from __InstanceModificationEvent WITHIN 10 WHERE TargetInstance ISA 'MSVM_GuestNetworkAdapterConfiguration'"));
            _networkWatcher.EventArrived += _networkWatcher_EventArrived;
            _networkWatcher.Start();


            _statusWatcher = new ManagementEventWatcher(
                scope,
                new WqlEventQuery(
                "__InstanceModificationEvent",
                    TimeSpan.FromSeconds(3),
                    "TargetInstance ISA 'Msvm_ComputerSystem' and TargetInstance.EnabledState <> PreviousInstance.EnabledState"));
            _statusWatcher.EventArrived += StatusWatcherOnEventArrived;
            _statusWatcher.Start();

            _vmWatcher = new ManagementEventWatcher(
                scope,
                new WqlEventQuery(
                    "__InstanceModificationEvent",
                    TimeSpan.FromSeconds(10),
                    "TargetInstance ISA 'Msvm_ComputerSystem'"));
            _vmWatcher.EventArrived += _vmWatcher_EventArrived;
            _vmWatcher.Start();

            _upTimeTimer.Change(TimeSpan.FromSeconds(UpTimeCheckSeconds), TimeSpan.Zero);
        }

        private void UpTimeCheck(object? state)
        {
            try
            {
                _upTimeTimer.Change(Timeout.Infinite, 0);

                // up time check only considers machines started within last hour.
                // for longer running machines the inventory job takes care of updating up time. 
                // It has to be only accurate during early start phase to check if deployment has succeeded and to handle sensitive data
                // timeout for cloud-init.
                using var vmSearcher = new ManagementObjectSearcher(@"\\.\root\virtualization\v2",
                    "SELECT Name,OnTimeInMilliseconds FROM MSVM_ComputerSystem where OnTimeInMilliseconds <> NULL AND OnTimeInMilliseconds < 3600000");

                using var vms = vmSearcher.Get();

                foreach (var vm in vms)
                {
                    var vmId = Guid.Parse(vm.GetPropertyValue("Name") as string
                                          ?? throw new InvalidOperationException());

                    var upTimeInMilliseconds = (ulong)vm.GetPropertyValue("OnTimeInMilliseconds");

                    _bus.SendWorkflowEvent(_workflowOptions,new CatletUpTimeChangedEvent()
                    {
                        VmId = vmId,
                        UpTime = TimeSpan.FromMilliseconds(upTimeInMilliseconds)
                    });

                }
            }
            finally
            {
                _upTimeTimer.Change(TimeSpan.FromSeconds(UpTimeCheckSeconds), TimeSpan.Zero);
            }
        }

        private void StatusWatcherOnEventArrived(object sender, EventArrivedEventArgs e)
        {
            _log.LogTrace("status event arrived: {EventMof}", e.NewEvent.GetText(TextFormat.Mof));

            var instance = GetTargetInstance(e);
            var vmId = Guid.Parse(instance.GetPropertyValue("Name") as string
                                  ?? throw new InvalidOperationException());

            var enabledState = (ushort) instance.GetPropertyValue("EnabledState");
            var otherEnabledState = (string) instance.GetPropertyValue("OtherEnabledState");
            var healthState = (ushort) instance.GetPropertyValue("HealthState");
            var timestampLong = (ulong)e.NewEvent.GetPropertyValue("TIME_CREATED");
            var timestamp = DateTime.FromFileTimeUtc((long) timestampLong);

            _bus.SendLocal(new VirtualMachineStateChangedEvent
            {
                VmId = vmId,
                State = StateConverter.ConvertVMState(enabledState, otherEnabledState, healthState),
                TimeStamp = timestamp
            });

        }

        private void _networkWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            _log.LogTrace("network event arrived: {EventMof}", e.NewEvent.GetText(TextFormat.Mof));

            using var instance = GetTargetInstance(e);

            var adapterId = instance.GetPropertyValue("InstanceID") as string;
            var pathParts = adapterId?.Split('\\');
            if (pathParts is not { Length: 3 })
                return;

            var vmId = Guid.Parse(pathParts[1]);
        }



        private void _vmWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            _log.LogTrace("vm change event arrived: {EventMof}", e.NewEvent.GetText(TextFormat.Mof));
            var targetInstance = GetTargetInstance(e);
            var vmId = GetVmId(targetInstance);

            // Skip the event when the VM is still being modified, i.e. the operational
            // status is in service. The inventory only needs to update when the change is
            // completed. Also, a lot of changes complete rather quickly. Hence, this
            // check limits the number of events which are raised in short succession.
            if (targetInstance["OperationalStatus"] is not ushort[] operationalStatus
                || operationalStatus.Length < 1
                || operationalStatus[0] == (ushort)VMComputerSystemOperationalStatus.InService)
            {
                return;
            }

            _log.LogTrace("Sending event...");
            _bus.SendLocal(new VirtualMachineChangedEvent
            {
                VmId = vmId,
            });
        }


        private static ManagementBaseObject GetTargetInstance(EventArrivedEventArgs e)
        {
            return (ManagementBaseObject)e.NewEvent["TargetInstance"];
        }

        private static Guid GetVmId(ManagementBaseObject instance)
        {
            if (instance["CreationClassName"] is "Msvm_ComputerSystem")
            {
                if (instance["name"] is not string name || !Guid.TryParse(name, out var vmId))
                    throw new ArgumentException(
                        "The management object does not contain a valid VM ID",
                        nameof(instance));

                return vmId;
            }


            var adapterId = instance.GetPropertyValue("InstanceID") as string;
            var pathParts = adapterId?.Split('\\');
            if (pathParts is not { Length: 3 })
                throw new InvalidOperationException("Invalid adapter id");

            return Guid.Parse(pathParts[1]);
        }

        /// <summary>
        /// This method handles events which are raised when a Hyper-V VM is changed.
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVmChanged(object sender, EventArrivedEventArgs e) =>
            GetVmId(e).IfSome(vmId =>
            {
                _bus.SendLocal(new VirtualMachineChangedEvent
                {
                    VmId = vmId,
                });
            });
        

        // TODO add proper error handling
        private static Option<Guid> GetVmId(EventArrivedEventArgs e) =>
            from _ in Some(unit)
            let targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"]
            from vmId in targetInstance.ClassPath.ClassName == "Msvm_ComputerSystem"
                // Msvm_ComputerSystem can be either the host or a VM. For VMs, the name
                // contains the Guid which identifies the VM.
                ? from vmId in parseGuid(targetInstance["Name"] as string)
                  select vmId
                : from _ in Some(unit)
                  let instanceId = (string)targetInstance["InstanceID"]
                  let parts = instanceId.Split('\\')
                  from idPart in parts.Length == 3 ? Some(parts[1]) : None
                  from vmId in parseGuid(idPart)
                  select vmId
            select vmId;
    }
}