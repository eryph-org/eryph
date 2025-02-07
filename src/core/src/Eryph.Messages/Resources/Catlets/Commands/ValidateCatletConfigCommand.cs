using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ValidateCatletConfigCommand
{
    public CatletConfig Config { get; set; }

    /// <summary>
    /// Indicates that the <see cref="Config"/> will
    /// be used to update an existing catlet.
    /// </summary>
    public bool IsUpdate { get; set; }
}
