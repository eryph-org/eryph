using System;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.VmHostAgent.Networks;

public record NetworkChangeOperation<RT>(
    NetworkChangeOperation Operation, 
    Func<Aff<RT, Unit>> Change,
    Func<Seq<NetworkChangeOperation>,bool>? CanRollBack,
    Func<Aff<RT, Unit>>? Rollback,
    params object[] Args)
    where RT : struct, HasCancel<RT>
{
    public string Text
    {
        get
        {
            try
            {
                return string.Format(NetworkChangeOperationNames.Instance[Operation], Args);

            }
            catch (Exception)
            {
                return NetworkChangeOperationNames.Instance[Operation];
            }
        }
    }
}

public enum NetworkChangeOperation
{
    StartOVN,
    StopOVN,

    CreateOverlaySwitch,
    RebuildOverLaySwitch,
    RemoveOverlaySwitch,

    DisconnectVMAdapters,
    ConnectVMAdapters,

    RemoveBridge,
    RemoveUnusedBridge,
    RemoveMissingBridge,
    AddBridge,

    AddNetNat,
    RemoveNetNat,

    RemoveAdapterPort,
    AddAdapterPort,
    AddBondPort,
    UpdateBondPort,
    UpdateBridgePort,

    ConfigureNatIp,
    UpdateBridgeMapping,

    EnableSwitchExtension,
    DisableSwitchExtension
}
