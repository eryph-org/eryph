using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class CatletConfigRedactorTests
{
    [Fact]
    public void RedactSecrets_WithSecrets_SecretsAreRedacted()
    {
        var config = new CatletConfig
        {
            Fodder = 
            [
                new FodderConfig
                {
                    Content = "secret content",
                    Secret = true,
                },
                new FodderConfig
                {
                    Content = "public content",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Value = "public fodder value",
                        },
                        new VariableConfig
                        {
                            Value = "secret fodder value",
                            Secret = true,
                        }
                    ]
                }
            ],
            Variables = 
            [
                new VariableConfig
                {
                    Value = "public value",
                },
                new VariableConfig
                {
                    Value = "secret value",
                    Secret = true,
                }
            ]
        };

        var result = CatletConfigRedactor.RedactSecrets(config);

        result.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be("#REDACTED"),
            fodder =>
            {
                fodder.Content.Should().Be("public content");
                fodder.Variables.Should().SatisfyRespectively(
                    variable => variable.Value.Should().Be("public fodder value"),
                    variable => variable.Value.Should().Be("#REDACTED"));
            });
        result.Variables.Should().SatisfyRespectively(
            variable => variable.Value.Should().Be("public value"),
            variable => variable.Value.Should().Be("#REDACTED"));
    }
}
