namespace Eryph.Messages.Components;

/// <summary>
/// The kind of component that registers with the controller. Determines which
/// configuration domains the component is entitled to receive.
/// </summary>
public enum ComponentType
{
    VMHostAgent,
    GenePoolAgent,
    Network,
    ComputeApi,
    Identity,
}
