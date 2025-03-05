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
    public void SubstituteVariables_CatletVariableInFodderContent_CatletVariableIsUsed()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "testCatletVariable",
                    Value = "test value"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Value is {{testCatletVariable}}!"
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be("Value is test value!"));
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
                    Name = "testVariable",
                    Value = "catlet test value"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Value is {{testVariable}}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "testVariable",
                            Value = "fodder test value",
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be("Value is fodder test value!"));
    }

    [Fact]
    public void SubstituteVariables_InvalidCatletVariableValue_ReturnsError()
    {
        var config = new CatletConfig()
        {
            Variables =
            [   
                new VariableConfig()
                {
                    Name = "testVariable",
                    Type = VariableType.Boolean,
                    Value = "invalid value",
                }
            ],
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Variables[Name=testVariable].Value");
                issue.Message.Should().Be(
                    "The value for the catlet variable 'testVariable' is invalid. The value is not a valid boolean. Only 'true' and 'false' are allowed.");
            });
    }

    [Fact]
    public void SubstituteVariables_InvalidFodderVariableValue_ReturnsError()
    {
        var config = new CatletConfig()
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "test-food",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "testVariable",
                            Type = VariableType.Boolean,
                            Value = "invalid value",
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Fodder[Source=gene:acme/acme-tools/1.0:test-fodder;Name=test-food].Variables[Name=testVariable].Value");
                issue.Message.Should().Be(
                    "The value for the variable 'testVariable' of the food 'test-food' from 'gene:acme/acme-tools/1.0:test-fodder' is invalid."
                    + " The value is not a valid boolean. Only 'true' and 'false' are allowed.");
            });
    }

    [Fact]
    public void SubstituteVariables_MissingRequiredCatletVariableValue_ReturnsError()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "testVariable",
                    Required = true,
                }
            ],
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Variables[Name=testVariable].Value");
                issue.Message.Should().Be(
                    "The value for the catlet variable 'testVariable' is required but missing.");
            });
    }

    [Fact]
    public void SubstituteVariables_MissingRequiredFodderVariableValue_ReturnsError()
    {
        var config = new CatletConfig()
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "test-food",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "testVariable",
                            Required = true,
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Fodder[Source=gene:acme/acme-tools/1.0:test-fodder;Name=test-food].Variables[Name=testVariable].Value");
                issue.Message.Should().Be(
                    "The value for the variable 'testVariable' of the food 'test-food' from 'gene:acme/acme-tools/1.0:test-fodder' is required but missing."
                    + " The variable should be bound to a catlet variable or a constant value.");
            });
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
                    Name = "testCatletVariable",
                    Value = "not a boolean"
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "test-food",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Content = "Value is {{ testFodderVariable }}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "testFodderVariable",
                            Type = VariableType.Boolean,
                            Value = "{{ testCatletVariable }}",

                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Fodder[Source=gene:acme/acme-tools/1.0:test-fodder;Name=test-food].Variables[Name=testFodderVariable].Value");
                issue.Message.Should().Be(
                    "The value for the variable 'testFodderVariable' of the food 'test-food' from 'gene:acme/acme-tools/1.0:test-fodder' is invalid."
                    + " The value is not a valid boolean. Only 'true' and 'false' are allowed.");
            });
    }

    [Fact]
    public void SubstituteVariables_MissingVariableInFodderContent_ReturnsError()
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
                    Name = "test-food",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
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

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Fodder[Source=gene:acme/acme-tools/1.0:test-fodder;Name=test-food].Content");
                issue.Message.Should().Be(
                    "The variable 'missingVariable' referenced by the food 'test-food' from 'gene:acme/acme-tools/1.0:test-fodder' does not exist.");
            });
    }

    [Fact]
    public void SubstituteVariables_MissingVariableInFodderVariableValue_ReturnsError()
    {
        var config = new CatletConfig()
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "test-food",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "testVariable",
                            Value = "{{missingVariable}}",
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Fodder[Source=gene:acme/acme-tools/1.0:test-fodder;Name=test-food].Variables[Name=testVariable].Value");
                issue.Message.Should().Be(
                    "The variable 'missingVariable' referenced by the food 'test-food' from 'gene:acme/acme-tools/1.0:test-fodder' does not exist.");
            });
    }

    [Fact]
    public void SubstituteVariables_CatletVariableIsSecret_FodderIsSecret()
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "testCatletVariable",
                    Value = "test value",
                    Secret = true,
                },
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Value is {{ testCatletVariable }}!",
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Content.Should().Be("Value is test value!");
                fodder.Secret.Should().BeTrue();
            });
    }

    [Fact]
    public void SubstituteVariables_FodderVariableIsSecret_FodderIsSecret()
    {
        var config = new CatletConfig()
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Value is {{ testFodderVariable }}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "testFodderVariable",
                            Value = "test value",
                            Secret = true,
                        },
                    ],
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Content.Should().Be("Value is test value!");
                fodder.Secret.Should().BeTrue();
            });
    }


    [Theory]
    [InlineData(VariableType.Boolean, "true")]
    [InlineData(VariableType.Number, "4.2")]
    [InlineData(VariableType.String, "test value")]
    public void SubstituteVariables_FodderVariableBoundToCatletVariable_CatletVariableValueIsUsed(
        VariableType variableType,
        string value)
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "testCatletVariable",
                    Value = value
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Value is {{ testFodderVariable }}!",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "testFodderVariable",
                            Type = variableType,
                            Value = "{{ testCatletVariable }}"
                        }
                    ]
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be($"Value is {value}!"));
    }

    [Theory]
    [InlineData(VariableType.Boolean, "false")]
    [InlineData(VariableType.Number, "0")]
    [InlineData(VariableType.String, "")]
    public void SubstituteVariables_VariableWithoutValue_DefaultValueIsUsed(
        VariableType variableType,
        string expectedValue)
    {
        var config = new CatletConfig()
        {
            Variables =
            [
                new VariableConfig()
                {
                    Name = "testVariable",
                    Type = variableType,
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Content = "Value is {{ testVariable }}!",
                }
            ]
        };

        var result = CatletConfigVariableSubstitutions.SubstituteVariables(config);

        var resultConfig = result.Should().BeSuccess().Subject;

        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Content.Should().Be($"Value is {expectedValue}!"));
    }
}
