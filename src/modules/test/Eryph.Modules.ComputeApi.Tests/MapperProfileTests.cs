using System;
using System.Collections.Generic;
using AutoMapper;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.ComputeApi.Model.V1;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests;

public class MapperProfileTests
{
    private static readonly IMapper Mapper = new MapperConfiguration(cfg =>
    {
        cfg.LicenseKey = AutoMapperLicense.Key;
        cfg.AddProfile<MapperProfile>();
    }, NullLoggerFactory.Instance).CreateMapper();

    [Fact]
    public void Variant_variables_are_projected_from_built_config()
    {
        var builtConfig = CatletConfigJsonSerializer.Serialize(new CatletConfig
        {
            Variables =
            [
                new VariableConfig
                {
                    Name = "size",
                    Type = VariableType.Number,
                    Value = "42",
                    Required = false,
                },
                // Only a secret flag is set; the other properties stay unset.
                new VariableConfig
                {
                    Name = "password",
                    Secret = true,
                },
            ],
        });

        var source = new Eryph.StateDb.Model.CatletSpecificationVersionVariant
        {
            Id = Guid.NewGuid(),
            SpecificationVersionId = Guid.NewGuid(),
            Architecture = Architecture.New("hyperv/amd64"),
            BuiltConfig = builtConfig,
            PinnedGenes = new List<Eryph.StateDb.Model.CatletSpecificationVersionVariantGene>(),
        };

        var result = Mapper.Map<CatletSpecificationVersionVariant>(source);

        result.Variables.Should().SatisfyRespectively(
            v =>
            {
                v.Name.Should().Be("size");
                v.Type.Should().Be(VariableType.Number);
                v.Value.Should().Be("42");
                v.Required.Should().BeFalse();
                v.Secret.Should().BeNull();
            },
            // Only a secret flag was set. Unset properties are passed through as
            // null instead of being defaulted - the API mirrors the built config.
            v =>
            {
                v.Name.Should().Be("password");
                v.Type.Should().BeNull();
                v.Secret.Should().BeTrue();
                v.Required.Should().BeNull();
                v.Value.Should().BeNull();
            });
    }

    [Fact]
    public void Variant_without_variables_maps_to_empty_list()
    {
        var builtConfig = CatletConfigJsonSerializer.Serialize(new CatletConfig
        {
            Name = "test",
        });

        var source = new Eryph.StateDb.Model.CatletSpecificationVersionVariant
        {
            Id = Guid.NewGuid(),
            SpecificationVersionId = Guid.NewGuid(),
            Architecture = Architecture.New("hyperv/amd64"),
            BuiltConfig = builtConfig,
            PinnedGenes = new List<Eryph.StateDb.Model.CatletSpecificationVersionVariantGene>(),
        };

        var result = Mapper.Map<CatletSpecificationVersionVariant>(source);

        result.Variables.Should().BeEmpty();
    }
}
