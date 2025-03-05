using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ValidateCatletConfigCommand
{
    public CatletConfig Config { get; set; }
}
