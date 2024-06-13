using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

namespace Eryph.VmManagement.Test;

public class CatletConfigVariableSubstitutionsTests
{
    [Fact]
    public void SubstituteVariables()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "nameOfAlice",
                    Value = "Alice"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Hello {{nameOfAlice}}!"
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be("Hello Alice!"));
    }

    [Fact]
    public void SubstituteVariables_VariableInCatletAndFodder_FodderVariableIsUsed()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "name",
                    Value = "Alice"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Hello {{name}}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "name",
                            Value = "Bob",
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be("Hello Bob!"));
    }

    [Fact]
    public void SubstituteVariables_InvalidValueForBoundVariable_SubstitutionFails()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "name",
                    Value = "Alice"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Price is {{name}}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "price",
                            Type = VariableType.Number,
                            Value = "{{name}}",

                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        result.Should().BeFail();

        //TODO Check error
    }

    [Fact]
    public void SubstituteVariables_MissingVariable_SubstitutionFails()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "firsVariable",
                    Value = "first value"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Price is {{missingVariable}}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "secondVariable",
                            Value = "second value",

                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var error = result.Should().BeFail().Subject;

        error.Should().NotBeEmpty();
        //TODO Check error
    }

    [Fact]
    public void SubstituteVariables2()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "nameOfAlice",
                    Value = "Alice"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Hello {{name}}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "name",
                            Value = "{{nameOfAlice}}"
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be("Hello Alice!"));
    }

    [Fact]
    public void SubstituteVariables3()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "nameOfAlice",
                    Value = "Alice"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Hello {{name}}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "name",
                            Value = "Alice"
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be("Hello Alice!"));
    }
}
