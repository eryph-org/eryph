﻿using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks;

public record HostState(
    Seq<VMSwitchExtension> VMSwitchExtensions,
    Seq<VMSwitch> VMSwitches,
    HostAdaptersInfo HostAdapters,
    Seq<NetNat> NetNat,
    OvsBridgesInfo OvsBridges);
