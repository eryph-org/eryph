using LanguageExt;

// ReSharper disable InconsistentNaming

namespace Eryph.Modules.VmHostAgent.Networks.OVS;


public interface HasOVSControl<RT>
    where RT : struct, HasOVSControl<RT>
{
    Eff<RT, IOVSControl> OVS { get; }
}