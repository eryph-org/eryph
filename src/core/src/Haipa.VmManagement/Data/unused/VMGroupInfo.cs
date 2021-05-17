using System;
using System.Collections.Generic;
using Haipa.VmManagement.Data.Full;

namespace Haipa.VmManagement.Data
{
    public sealed class VMGroupInfo
    {
        public string Name { get; set; }


        public Guid InstanceId { get; set; }


        public GroupType GroupType { get; set; }


        public IReadOnlyList<VirtualMachineInfo> VMMembers { get; set; }


        public IReadOnlyList<VMGroupInfo> VMGroupMembers { get; set; }
    }
}