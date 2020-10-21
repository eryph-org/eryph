using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Haipa.StateDb.Model
{
    public class MachineNetwork
    {
        public Guid Id { get; set; }

        public Guid MachineId { get; set; }
        public Machine Machine { get; set; }

        public string Name { get; set; }

        [Column("IpV4Addresses")]
        public string IpV4AddressesInternal { get; set; }

        [NotMapped]
        public string[] IpV4Addresses
        {
            get => IpV4AddressesInternal?.Split(';');
            set => IpV4AddressesInternal = value == null ? null : string.Join(";",value );
        }

        [Column("IpV6Addresses")]
        public string IpV6AddressesInternal { get; set; }

        [NotMapped]
        public string[] IpV6Addresses
        {
            get => IpV6AddressesInternal?.Split(';');
            set => IpV6AddressesInternal = value == null ? null : string.Join(";", value);
        }

        // ReSharper disable once InconsistentNaming
        public string IPv4DefaultGateway { get; set; }
        // ReSharper disable once InconsistentNaming
        public string IPv6DefaultGateway { get; set; }



        [Column("DnsServers")]
        public string DnsServersInternal { get; set; }

        [NotMapped]
        public string[] DnsServerAddresses
        {
            get => DnsServersInternal?.Split(';');
            set => DnsServersInternal = value == null ? null : string.Join(";", value);
        }

        [Column("IpV4Subnets")]
        public string IpV4SubnetsInternal { get; set; }

        [NotMapped]
        public string[] IpV4Subnets
        {
            get => IpV4SubnetsInternal?.Split(';');
            set => IpV4SubnetsInternal = value == null ? null : string.Join(";",value );
        }

        [Column("IpV6Subnets")]
        public string IpV6SubnetsInternal { get; set; }

        [NotMapped]
        public string[] IpV6Subnets
        {
            get => IpV6SubnetsInternal?.Split(';');
            set => IpV6SubnetsInternal = value == null ? null : string.Join(";", value);
        }

    }
}