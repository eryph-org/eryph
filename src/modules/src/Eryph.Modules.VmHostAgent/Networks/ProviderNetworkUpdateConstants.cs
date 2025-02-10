namespace Eryph.Modules.VmHostAgent.Networks;

internal static class ProviderNetworkUpdateConstants
{
    internal static readonly NetworkChangeOperation[] UnsafeChanges =
    [
        NetworkChangeOperation.RemoveAdapterPort,
        NetworkChangeOperation.AddAdapterPort,
        NetworkChangeOperation.AddBondPort,

        NetworkChangeOperation.RebuildOverLaySwitch,
        NetworkChangeOperation.UpdateBridgePort,
        NetworkChangeOperation.DisableSwitchExtension
    ];
}