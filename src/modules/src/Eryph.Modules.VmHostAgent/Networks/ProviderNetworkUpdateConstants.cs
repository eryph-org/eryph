namespace Eryph.Modules.VmHostAgent.Networks;

internal static class ProviderNetworkUpdateConstants
{
    internal static readonly NetworkChangeOperation[] UnsafeChanges = new[]
    {
        NetworkChangeOperation.RemoveAdapterPort,
        NetworkChangeOperation.AddAdapterPort,
        NetworkChangeOperation.RebuildOverLaySwitch,
        NetworkChangeOperation.UpdateBridgePort,
    };
}