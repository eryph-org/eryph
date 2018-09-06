using System;
using System.Collections.Generic;

namespace HyperVPlus.Messages
{
    public class VmInventoryInfo
    {

        public string Name { get; set; }

        public Guid Id { get; set; }
        
        public VmStatus Status { get; set; }

        public List<string> IpV4Addresses { get; set; }
        public List<string> IpV6Addresses { get; set; }

    }

    public enum VmStatus
    {
        Stopped,
        Running
    }
}