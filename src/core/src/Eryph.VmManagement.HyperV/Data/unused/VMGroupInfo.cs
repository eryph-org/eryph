using System;
using System.Collections.Generic;
using Eryph.VmManagement.Data.Full;

namespace Eryph.VmManagement.Data
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