using System;
using System.Collections;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
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

        public WmiWatcherModuleService(IBus bus, ILogger log)
        {
            _bus = bus;
            _log = log;
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

            var instance = GetTargetInstance(e);

            var adapterId = instance.GetPropertyValue("InstanceID") as string;
            var pathParts = adapterId?.Split('\\');
            if (pathParts == null || pathParts.Length != 3)
                return;

            var vmId = Guid.Parse(pathParts[1]);

            _bus.SendLocal(new GuestNetworkAdapterChangedEvent
            {
                VmId = vmId,
                AdapterId = adapterId,
                IPAddresses = ObjectToStringArray(instance.GetPropertyValue("IPAddresses")),
                Netmasks = ObjectToStringArray(instance.GetPropertyValue("Subnets")),
                DefaultGateways = ObjectToStringArray(instance.GetPropertyValue("DefaultGateways")),
                DnsServers = ObjectToStringArray(instance.GetPropertyValue("DNSServers")),
                DhcpEnabled = (bool) instance.GetPropertyValue("DHCPEnabled")
            });
        }

        private static string[] ObjectToStringArray(object value)
        {
            if (value != null && value is IEnumerable enumerable) return enumerable.Cast<string>().ToArray();

            return new string[0];
        }

        private static ManagementBaseObject GetTargetInstance(EventArrivedEventArgs e)
        {
            return e.NewEvent.GetPropertyValue("TargetInstance") as ManagementBaseObject;
        }
    }
}