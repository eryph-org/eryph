using System;
using System.Security.Cryptography;
using System.Text;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// The identity a module uses when registering with the controller.
/// <see cref="ComponentId"/> is stable across restarts (derived deterministically
/// from the component type and machine name, not a random per-run value);
/// <see cref="InstanceId"/> identifies this particular run.
/// </summary>
public sealed class ComponentIdentity
{
    public ComponentIdentity(ComponentType componentType, string inboundQueue)
    {
        ComponentType = componentType;
        InboundQueue = inboundQueue;
        MachineName = Environment.MachineName;
        ComponentId = CreateStableId(componentType, MachineName);
        InstanceId = Guid.NewGuid();
    }

    public Guid ComponentId { get; }

    public Guid InstanceId { get; }

    public ComponentType ComponentType { get; }

    public string InboundQueue { get; }

    public string MachineName { get; }

    private static Guid CreateStableId(ComponentType componentType, string machineName)
    {
        var name = $"eryph-component:{componentType}:{machineName}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(name));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
