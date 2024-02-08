using System;
using System.Linq;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class DestroyVirtualNetworksCommand : IHasResources, ICommandWithName
{
    public Guid[] NetworkIds { get; set; }
    public string GetCommandName() => "Destroy Virtual Networks";
    public Resource[] Resources => NetworkIds.Select(id => new Resource(ResourceType.VirtualNetwork, id)).ToArray();
}