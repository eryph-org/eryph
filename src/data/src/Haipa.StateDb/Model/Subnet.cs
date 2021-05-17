using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Haipa.StateDb.Model
{
    public class Subnet
    {
        public Guid Id { get; set; }

        public Network Network { get; set; }
        public Guid NetworkId { get; set; }

        public bool IsPublic { get; set; }

        public bool DhcpEnabled { get; set; }
        public byte IpVersion { get; set; }
        public string GatewayAddress { get; set; }
        public string Address { get; set; }


        [Column("DnsServers")]
        public string DnsServersInternal { get; set; }

        [NotMapped]
        public string[] DnsServerAddresses
        {
            get => DnsServersInternal?.Split(';');
            set => DnsServersInternal = value == null ? null : string.Join(";", value);
        }
    }
}