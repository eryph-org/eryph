using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class MachineNetwork
    {
        [Key] public Guid Id { get; set; }

        public string MachineId { get; set; }

        public string Name { get; set; }


        public IEnumerable<string> IpV4Addresses { get; set; }
        public IEnumerable<string> IpV6Addresses { get; set; }

        // ReSharper disable once InconsistentNaming
        public string IPv4DefaultGateway { get; set; }

        // ReSharper disable once InconsistentNaming
        public string IPv6DefaultGateway { get; set; }

        public IEnumerable<string> DnsServerAddresses { get; set; }
        public IEnumerable<string> IpV4Subnets { get; set; }
        public IEnumerable<string> IpV6Subnets { get; set; }
    }
}