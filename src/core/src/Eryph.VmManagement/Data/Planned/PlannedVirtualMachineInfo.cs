﻿using System;
using Eryph.ConfigModel;
using Eryph.VmManagement.Data.Core;
using LanguageExt;

namespace Eryph.VmManagement.Data.Planned
{
    public sealed class PlannedVirtualMachineInfo : Record<PlannedVirtualMachineInfo>,
        IVirtualMachineCoreInfo, 
        IVMWithNetworkAdapterInfo,
        IVMWithDrivesInfo

    {

        public bool DynamicMemoryEnabled { get; private set; }

        public long MemoryMaximum { get; private set; }

        public long MemoryMinimum { get; private set; }
        public Guid Id { get; private set; }

        public string Name { get; private set; }

        public long ProcessorCount { get; private set; }
        public int Generation { get; private set; }

        [PrivateIdentifier]

        public string Path { get; private set; }
        public long MemoryStartup { get; private set; }


        public VirtualMachineDeviceInfo[] DVDDrives { get; private set; }
        public VirtualMachineDeviceInfo[] HardDrives { get; private set; }

        public VirtualMachineDeviceInfo[] NetworkAdapters { get; private set; }
    }
}