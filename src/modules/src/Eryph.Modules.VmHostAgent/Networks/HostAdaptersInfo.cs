using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly record struct HostAdaptersInfo(
    HashMap<string, HostAdapterInfo> Adapters);

public readonly record struct HostAdapterInfo(
    string Name,
    Guid InterfaceId,
    Option<string> ConfiguredName,
    bool IsPhysical);
