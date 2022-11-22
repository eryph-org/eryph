using System;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks.Powershell;

public readonly record struct OverlaySwitchInfo(Guid Id, HashSet<string> AdaptersInSwitch);