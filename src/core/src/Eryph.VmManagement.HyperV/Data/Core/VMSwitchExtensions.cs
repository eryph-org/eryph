using System;
using System.Collections.Generic;
using System.Reflection;

namespace Eryph.VmManagement.Data.Core
{
    public static class VMSwitchExtensions
    {
        public static Guid[] GetNetAdapterInterfaceGuid(this TypedPsObject<VMSwitch> vmSwitch)
        {
            // It is possible that Hyper-V report a switch embedded team without interfaces.
            // We need to handle this situation gracefully.
            if (vmSwitch.Value.NetAdapterInterfaceDescriptions is not { Length: > 0 }
                || vmSwitch.Value.NetAdapterInterfaceGuid != null)
            {
                return vmSwitch.Value.NetAdapterInterfaceGuid;
            }
            
            // NetAdapterInterfaceGuid as property is not available, so fetch it via reflection
            // this has been added for Server 2016 compatibility so it can be removed
            // when 2016 is out of extended support (year 2027)
            // newer windows version expose this property directly
            var baseObject = vmSwitch.PsObject?.BaseObject;
            if (baseObject != null)
            {
                var getAdaptersMethod = baseObject.GetType().GetMethod("GetAndOrganizeNetworkAdapters",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (getAdaptersMethod != null)
                {
                    var param = new object[2];
                    getAdaptersMethod.Invoke(baseObject, param);
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (param[0] != null)
                    {
                        var externalPort = param[0];

                        var externalAdapterAccessorField = externalPort.GetType()
                            .GetField("m_ExternalNetworkAdapters",
                                BindingFlags.Instance | BindingFlags.NonPublic);

                        object[] externalAdapters = null;
                        if (externalAdapterAccessorField != null)
                        {
                            // there is either a list of external network adapters 
                            var externalAdapterAccessor = externalAdapterAccessorField.GetValue(externalPort);
                            var externalAdapterAccessorDataField = externalAdapterAccessor?.GetType().BaseType?
                                .GetField("m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

                            externalAdapters =
                                externalAdapterAccessorDataField?.GetValue(externalAdapterAccessor) as object[];
                        }
                        else
                        {
                            //or a single external adapter field
                            externalAdapterAccessorField = externalPort.GetType()
                                .GetField("m_ExternalNetworkAdapter",
                                    BindingFlags.Instance | BindingFlags.NonPublic);

                            var externalAdapterAccessor = externalAdapterAccessorField?.GetValue(externalPort);
                            var externalAdapterAccessorDataField = externalAdapterAccessor?.GetType().BaseType?
                                .GetField("m_Value", BindingFlags.Instance | BindingFlags.NonPublic);

                            var externalAdapter =
                                externalAdapterAccessorDataField?.GetValue(externalAdapterAccessor);
                            if (externalAdapter != null)
                                externalAdapters = new[] { externalAdapter };
                        }

                        if (externalAdapters != null)
                        {
                            var deviceList = new List<Guid>();
                            foreach (var externalAdapter in externalAdapters)
                            {
                                var deviceIdProperty = externalAdapter.GetType().GetProperty("DeviceId");
                                var deviceId = deviceIdProperty?.GetValue(externalAdapter) as string;
                                if (string.IsNullOrWhiteSpace(deviceId)) continue;

                                deviceId = deviceId.Replace("Microsoft:", string.Empty);
                                if (Guid.TryParse(deviceId, out var deviceGuid))
                                {
                                    deviceList.Add(deviceGuid);
                                }
                            }

                            return deviceList.ToArray();
                        }
                    }
                }
            }

            throw new Exception($"Failed to identify physical adapters of switch '{vmSwitch.Value.Name}'");
        }
    }
}
