namespace Eryph.Modules.HostAgent.Networks;

internal static class ProviderNetworkUpdateConstants
{
    internal static readonly NetworkChangeOperation[] UnsafeChanges =
    [
        NetworkChangeOperation.RemoveAdapterPort,
        NetworkChangeOperation.AddAdapterPort,
        NetworkChangeOperation.AddBondPort,
        NetworkChangeOperation.UpdateBondPort,

        NetworkChangeOperation.RebuildOverLaySwitch,
        NetworkChangeOperation.UpdateBridgePort,
        NetworkChangeOperation.DisableSwitchExtension
    ];
}