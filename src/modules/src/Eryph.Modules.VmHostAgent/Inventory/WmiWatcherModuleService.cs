using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Events;
using Eryph.VmManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    internal class WmiWatcherModuleService : IHostedService
    {
        private readonly IBus _bus;
        private readonly ILogger _log;

        private ManagementEventWatcher _networkWatcher;
        private ManagementEventWatcher _statusWatcher;
        private readonly Timer _upTimeTimer;

        private const int UpTimeCheckSeconds= 60;

        public WmiWatcherModuleService(IBus bus, ILogger log)
        {
            _bus = bus;
            _log = log;

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
            _networkWatcher = null;

            _statusWatcher?.Dispose();
            _statusWatcher = null;

            _upTimeTimer.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }



        public void StartWatching()
        {
            _log.LogDebug("Starting WMI Watcher");
            var scope = new ManagementScope(@"\\.\root\virtualization\v2");

            var query =
                new WqlEventQuery(
                    "Select * from __InstanceModificationEvent WITHIN 10 WHERE TargetInstance ISA 'MSVM_GuestNetworkAdapterConfiguration'");

            _networkWatcher = new ManagementEventWatcher(scope, query);
            _networkWatcher.EventArrived += _networkWatcher_EventArrived;
            _networkWatcher.Start();

            query =
                new WqlEventQuery(
                    "Select * from __InstanceModificationEvent within 3 where TargetInstance ISA 'MSVM_ComputerSystem' and TargetInstance.EnabledState <> PreviousInstance.EnabledState");

            _statusWatcher = new ManagementEventWatcher(scope, query);
            _statusWatcher.EventArrived += StatusWatcherOnEventArrived;
            _statusWatcher.Start();

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

                    _bus.Publish(new VMUpTimeChangedEvent()
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

            _bus.SendLocal(new VirtualMachineStateChangedEvent
            {
                VmId = vmId,
                State = StateConverter.ConvertVMState(enabledState, otherEnabledState, healthState)
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

            _bus.SendLocal(new GuestNetworkAdapterChangedEvent
            {
                VmId = vmId,
                AdapterId = adapterId,
            });
        }

        private static ManagementBaseObject GetTargetInstance(EventArrivedEventArgs e)
        {
            return e.NewEvent.GetPropertyValue("TargetInstance") as ManagementBaseObject;
        }

    }

}