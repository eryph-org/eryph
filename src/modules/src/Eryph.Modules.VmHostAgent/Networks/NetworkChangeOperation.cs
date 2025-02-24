using System;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.VmHostAgent.Networks;

/// <summary>
/// Holds a network change operation.
/// </summary>
/// <param name="Operation">
/// The type of operation
/// </param>
/// <param name="Change">
/// The function which applies the change.
/// </param>
/// <param name="CanRollBack">
/// Indicates whether the change can be rolled back.
/// </param>
/// <param name="Rollback">
/// The function which rolls back the change.
/// </param>
/// <param name="Force">
/// Indicates that the change should be applied automatically
/// even if the operation is considered unsafe.
/// </param>
public record NetworkChangeOperation<RT>(
    NetworkChangeOperation Operation, 
    Func<Aff<RT, Unit>> Change,
    Func<Seq<NetworkChangeOperation>,bool>? CanRollBack,
    Func<Aff<RT, Unit>>? Rollback,
    bool Force,
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
