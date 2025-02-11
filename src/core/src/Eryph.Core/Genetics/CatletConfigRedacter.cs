using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;

namespace Eryph.Core.Genetics;

public static class CatletConfigRedactor
{
    public static CatletConfig RedactSecrets(
        CatletConfig catletConfig) =>
        catletConfig.CloneWith(c =>
        {
            c.Fodder = c.Fodder.ToSeq()
                .Map(RedactSecrets)
                .ToArray();
            c.Variables = c.Variables.ToSeq()
                .Map(RedactSecrets)
                .ToArray();
        });

    private static FodderConfig RedactSecrets(
        FodderConfig fodderConfig) =>
        fodderConfig.CloneWith(c =>
        {
            c.Content = c.Secret.GetValueOrDefault()
                ? "#REDACTED"
                : c.Content;
            c.Variables = c.Variables.ToSeq()
                .Map(RedactSecrets)
                .ToArray();
        });

    private static VariableConfig RedactSecrets(
        VariableConfig config) =>
        config.CloneWith(c =>
        {
            c.Value = c.Secret.GetValueOrDefault()
                ? "#REDACTED"
                : c.Value;
        });
}