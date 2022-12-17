using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateCatletNetworksCommand
{
    public Guid ProjectId { get; set; }
    public CatletConfig Config { get; set; }
    public Guid CatletId { get; set; }
}