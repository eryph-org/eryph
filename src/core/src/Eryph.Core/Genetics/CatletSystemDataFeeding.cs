using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;

namespace Eryph.Core.Genetics;

public static class CatletSystemDataFeeding
{
    public static CatletConfig FeedSystemVariables(
        CatletConfig config,
        Guid catletId,
        Guid vmId) =>
        FeedSystemVariables(config, catletId.ToString(), vmId.ToString());

    public static CatletConfig FeedSystemVariables(
        CatletConfig config,
        string catletId,
        string vmId) =>
        config.CloneWith(c =>
        {
            c.Variables =
            [
                ..c.Variables ?? [],
                new VariableConfig
                {
                    Name = EryphConstants.SystemVariables.CatletId,
                    Type = VariableType.String,
                    Value = catletId,
                    Required = false,
                    Secret = false,
                },
                new VariableConfig
                {
                    Name = EryphConstants.SystemVariables.VmId,
                    Type = VariableType.String,
                    Value = vmId,
                    Required = false,
                    Secret = false,
                },
            ];
        });
}
