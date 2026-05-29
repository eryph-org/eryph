using System;
using Eryph.Core;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller;

/// <summary>
/// Single-host implementation used by eryph-zero. Returns exactly the values the
/// previous hard-coded providers used — the local machine name and the single
/// local OVN chassis — so introducing the registry seam changes no behavior.
/// </summary>
internal sealed class SingleHostComponentRegistry : IComponentRegistry
{
    public Seq<HostAgentComponent> GetHostAgents() =>
        Seq1(new HostAgentComponent(
            Environment.MachineName,
            EryphConstants.Networking.LocalChassisName,
            1));
}
