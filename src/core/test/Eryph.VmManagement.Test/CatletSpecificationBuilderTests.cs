using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.VmManagement.TestBase;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Moq;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class CatletSpecificationBuilderTests
{
    private readonly MockGenePoolReader _genepoolReaderMock = new();

    [Fact]
    public async Task Build_ConfigWithoutParent_ReturnsBuiltConfig()
    {
        var config = new CatletConfig();

        var either = await CatletSpecificationBuilder.Build(
            config,
            Architecture.New("hyperv/amd64"),
            _genepoolReaderMock.Object,
            CancellationToken.None);
        var result = either.Should().BeRight().Subject;

        result.Config.ConfigType.Should().Be(CatletConfigType.Specification);
    }

    [Fact]
    public async Task Build_ConfigWithRemovedDefaultNetwork_ReturnsConfigWithoutDefaultNetwork()
    {
        var config = new CatletConfig
        {
            Networks = 
            [
                new CatletNetworkConfig
                {
                    Name = EryphConstants.DefaultNetworkName,
                    Mutation = MutationType.Remove,
                },
            ],
        };

        var either = await CatletSpecificationBuilder.Build(
            config,
            Architecture.New("hyperv/amd64"),
            _genepoolReaderMock.Object,
            CancellationToken.None);
        var result = either.Should().BeRight().Subject;

        result.Config.ConfigType.Should().Be(CatletConfigType.Specification);
        result.Config.Networks.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ResolveConfig_ConfigWithGeneSetTagReferences_ReturnsResolvedData()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/starter",
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/acme-images:first-image",
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools:test-fodder",
                }
            ],
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/starter-1.0",
            new CatletConfig()
            {
                Name = "acme-os-starter",
                Parent = "acme/acme-os/latest",
                Fodder =
                [
                    new FodderConfig()
                    {
                        Source = "gene:acme/acme-tools:other-test-fodder",
                    }
                ]
            });

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/1.0",
            new CatletConfig()
            {
                Name = "acme-os-base",
            });

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter", "acme/acme-os/starter-1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter-1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/latest", "acme/acme-os/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-images/latest", "acme/acme-images/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-images/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/latest", "acme/acme-tools/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);
        var result = either.Should().BeRight().Subject;

        var resolvedGeneSets = result.ResolvedGeneSets.ToDictionary();
        var resolvedParents = result.ResolvedCatlets.ToDictionary();


        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-images/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-images/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-tools/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-tools/1.0"));

        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Name.Should().Be("acme-os-base");
        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Name.Should().Be("acme-os-starter");

        // Gene sets should only be resolved exactly once.
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/starter"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/latest"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-images/latest"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-tools/latest"), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Resolve_ConfigWithResolvedGeneSetTags_ReturnsResolvedData()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/starter-1.0",
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/acme-images/1.0:first-image",
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                }
            ],
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/starter-1.0",
            new CatletConfig()
            {
                Name = "acme-os-starter",
                Parent = "acme/acme-os/1.0",
                Fodder =
                [
                    new FodderConfig()
                    {
                        Source = "gene:acme/acme-tools/1.0:other-test-fodder",
                    }
                ]
            });

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/1.0",
            new CatletConfig()
            {
                Name = "acme-os-base",
            });

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter-1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-images/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);
        var result = either.Should().BeRight().Subject;

        var resolvedGeneSets = result.ResolvedGeneSets.ToDictionary();
        var resolvedParents = result.ResolvedCatlets.ToDictionary();

        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-images/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-images/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-tools/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-tools/1.0"));

        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Name.Should().Be("acme-os-base");
        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Name.Should().Be("acme-os-starter");

        // Gene sets should only be resolved exactly once.
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-images/1.0"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-tools/1.0"), CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData("acme/acme-os/latest")]
    [InlineData("acme/acme-os/1.0")]
    public async Task ResolveConfig_ChildUsesDriveGeneFromParent_ResolvedDriveSourceIsIncluded(string parentId)
    {
        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/1.0",
            new CatletConfig()
            {
                Name = "acme-os",
                Drives =
                [
                    new CatletDriveConfig
                    {
                        Name = "sda",
                    }
                ],
            });

        _genepoolReaderMock.SetupVolumeGene("gene:acme/acme-os/1.0:sda", "hyperv/amd64", "sda");

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/latest", "acme/acme-os/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/1.0", None);

        
        var config = new CatletConfig
        {
            Name = "catlet",
            Parent = parentId,
        };

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        either.Should().BeRight().Which.ResolvedCatlets.ToDictionary()
            .Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0")).WhoseValue.Drives
            .Should().SatisfyRespectively(
                drive =>
                {
                    drive.Name.Should().Be("sda");
                    drive.Source.Should().Be("gene:acme/acme-os/1.0:sda");
                });
    }

    /// <summary>
    /// Replication for https://github.com/eryph-org/eryph/issues/352
    /// </summary>
    [Theory]
    [InlineData("acme/acme-os/latest")]
    [InlineData("acme/acme-os/1.0")]
    public async Task ResolveConfig_ParentHasDriveWhichIsNotAGene_DriveIsIncludedWithoutSource(string parentId)
    {
        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/1.0",
            new CatletConfig()
            {
                Name = "acme-os",
                Drives =
                [
                    new CatletDriveConfig
                    {
                        Name = "sda",
                    }
                ],
            });

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/latest", "acme/acme-os/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/1.0", None);


        var config = new CatletConfig
        {
            Name = "catlet",
            Parent = parentId,
        };

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        either.Should().BeRight().Which.ResolvedCatlets.ToDictionary()
            .Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0")).WhoseValue.Drives
            .Should().SatisfyRespectively(
                drive =>
                {
                    drive.Name.Should().Be("sda");
                    drive.Source.Should().BeNull();
                });
    }

    [Theory]
    [InlineData("acme/acme-os/latest")]
    [InlineData("acme/acme-os/1.0")]
    public async Task ResolveConfig_ChildUsesFodderFromParent_ResolvedFodderSourceIsIncluded(string parentId)
    {
        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/1.0",
            new CatletConfig()
            {
                Name = "acme-os",
                Fodder =
                [
                    new FodderConfig()
                    {
                        Name = "parent-fodder",
                        Content = "parent fodder content",
                    }
                ]
            });

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/latest", "acme/acme-os/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/1.0", None);



        var config = new CatletConfig
        {
            Name = "catlet",
            Parent = parentId,
        };


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        either.Should().BeRight().Which.ResolvedCatlets.ToDictionary()
            .Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0")).WhoseValue.Fodder
            .Should().SatisfyRespectively(
                fodder => fodder.Source.Should().Be("gene:acme/acme-os/1.0:catlet"));
    }

    [Fact]
    public async Task ResolveConfig_DriveSourceIsAPath_IgnoresDriveSource()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = @"Z:\test\test.vhdx",
                }
            ],
        };

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);
        
        var result = either.Should().BeRight().Subject;
        result.ResolvedCatlets.Should().BeEmpty();
        result.ResolvedGeneSets.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveConfig_MissingGrandParent_ReturnsError()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/starter-1.0",
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/starter-1.0",
            new CatletConfig()
            {
                Name = "acme-os-starter",
                Parent = "acme/acme-os/1.0",
            });

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter-1.0", None);


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the ancestor catlet -> acme/acme-os/starter-1.0.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-os/1.0'.");
    }

    [Fact]
    public async Task ResolveConfig_MissingDriveSource_ReturnsError()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/acme-images/1.0:first-image",
                }
            ],
        };

        
        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the catlet config.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-images/1.0'.");
    }

    [Fact]
    public async Task ResolveConfig_MissingFodderSource_ReturnsError()
    {
        var config = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                }
            ],
        };

        
        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the catlet config.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-tools/1.0'.");
    }

    [Fact]
    public async Task ResolveConfig_AncestorsHaveCircle_ReturnsError()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/first",
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/first-1.0",
            new CatletConfig()
            {
                Name = "acme-os-first-1.0",
                Parent = "acme/acme-os/second",
            });

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/second-1.0",
            new CatletConfig()
            {
                Name = "acme-os-second-1.0",
                Parent = "acme/acme-os/first",
            });

        
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/first", "acme/acme-os/first-1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/first-1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/second", "acme/acme-os/second-1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/second-1.0", None);

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be(
            "Could not resolve genes in the ancestor catlet "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0) "
            + "-> (acme/acme-os/second -> acme/acme-os/second-1.0) "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0).");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "The pedigree contains a circle.");
    }
}
