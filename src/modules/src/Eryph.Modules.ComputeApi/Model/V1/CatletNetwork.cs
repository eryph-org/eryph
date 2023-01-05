using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class CatletNetwork
    {

        public string Name { get; set; }
        public string Provider { get; set; }

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