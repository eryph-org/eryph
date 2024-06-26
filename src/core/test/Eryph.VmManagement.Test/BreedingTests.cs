using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using FluentAssertions;
using Xunit;

namespace Eryph.VmManagement.Test;

public class BreedingTests
{
    [Fact]
    public void Child_get_parent_attributes()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Capabilities =
            [
                new CatletCapabilityConfig
                {
                    Name = "Cap1"
                }
            ],
            Cpu = new CatletCpuConfig { Count = 2 },
            Drives =
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.VHD,
                    Store = "lair",
                    Size = 100
                }
            ],
            Memory = new CatletMemoryConfig { Startup = 2048 },
            NetworkAdapters =
            [
                new CatletNetworkAdapterConfig
                {
                    Name = "eth0"
                }
            ],
            Networks =
            [
                new CatletNetworkConfig
                {
                    Name = "default",
                    AdapterName = "eth0",
                    SubnetV4 = new CatletSubnetConfig
                    {
                        Name = "sub1",
                        IpPool = "pool1"
                    }
                }
            ],
            Fodder =
            [
                new FodderConfig
                {
                    Source = "food_from_somewhere_else"
                }
            ],
            Variables =
            [
                new VariableConfig() { Name = "parentCatletVariable" }
            ],
        };

        var child = new CatletConfig
        {
            Name = "child",
            Project = "social",
            Environment = "env1",
            Drives = [],
            Networks =
            [
                new CatletNetworkConfig
                {
                    Name = "default",
                    AdapterName = "eth0"
                }
            ],
            Fodder =
            [
                new FodderConfig { Name = "food" }
            ],
            Variables =
            [
                new VariableConfig() { Name = "catletVariable" }
            ]
        };
        var breedChild = parent.Breed(child, "reference");

        breedChild.Parent.Should().Be("reference");
        breedChild.Project.Should().Be("social");
        breedChild.Environment.Should().Be("env1");

        breedChild.Capabilities.Should().NotBeNull();
        breedChild.Capabilities.Should().BeEquivalentTo(parent.Capabilities);
        breedChild.Capabilities.Should().NotBeSameAs(parent.Capabilities);

        breedChild.Drives.Should().NotBeNull();
        breedChild.Drives.Should().HaveCount(1);
        breedChild.Drives.Should().NotBeEquivalentTo(parent.Drives);
        breedChild.Drives?[0].Source.Should().Be("gene:reference:sda");
        breedChild.Drives.Should().NotBeSameAs(parent.Drives);

        breedChild.NetworkAdapters.Should().NotBeNull();
        breedChild.NetworkAdapters.Should().HaveCount(1);
        breedChild.NetworkAdapters.Should().BeEquivalentTo(parent.NetworkAdapters);
        breedChild.NetworkAdapters.Should().NotBeSameAs(parent.NetworkAdapters);

        breedChild.Networks.Should().NotBeNull();
        breedChild.Networks.Should().HaveCount(1);
        breedChild.Networks.Should().BeEquivalentTo(parent.Networks);
        breedChild.Networks.Should().NotBeSameAs(parent.Networks);

        breedChild.Fodder.Should().NotBeNull();
        breedChild.Fodder.Should().HaveCount(2);
        breedChild.Fodder?[0].Source.Should().Be("food_from_somewhere_else");

        breedChild.Variables.Should().SatisfyRespectively(
            variable => variable.Name.Should().Be("catletVariable"),
            variable => variable.Name.Should().Be("parentCatletVariable"));
    }

    [Fact]
    public void Capabilities_are_merged()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Capabilities = new[]
            {
                new CatletCapabilityConfig
                {
                    Name = "Cap1",
                    Details = new[] { "detail" }
                }
            }
        };

        var child = new CatletConfig
        {
            Name = "child",
            Capabilities = new[]
            {
                new CatletCapabilityConfig
                {
                    Name = "Cap1",
                    Details = new[] { "detail2" }
                }
            }
        };

        var breedChild = parent.Breed(child);

        breedChild.Capabilities.Should().NotBeNull();
        breedChild.Capabilities.Should().NotBeEquivalentTo(parent.Capabilities);
        breedChild.Capabilities.Should().HaveCount(1);
        breedChild.Capabilities?[0].Details.Should().BeEquivalentTo(new[] { "detail2" });


    }

    [Fact]
    public void Drives_are_merged()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.VHD
                },
                new CatletDriveConfig
                {
                    Name = "sdb",
                    Type = CatletDriveType.VHD,
                }
            }
        };

        var child = new CatletConfig
        {
            Name = "child",
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Name = "sda",
                    Store = "none",
                },
                new CatletDriveConfig
                {
                    Name = "sdb",
                    Type = CatletDriveType.PHD,
                    Store = "none",
                    Location = "peng"
                }
            }
        };

        var breedChild = parent.Breed(child, "reference");

        breedChild.Drives.Should().NotBeNull();
        breedChild.Drives.Should().NotBeEquivalentTo(parent.Drives);
        breedChild.Drives.Should().HaveCount(2);
        breedChild.Drives?[0].Type.Should().Be(CatletDriveType.VHD);
        breedChild.Drives?[0].Store.Should().Be("none");
        breedChild.Drives?[0].Source.Should().Be("gene:reference:sda");
        breedChild.Drives?[1].Type.Should().Be(CatletDriveType.PHD);
        breedChild.Drives?[1].Store.Should().Be("none");
        breedChild.Drives?[1].Source.Should().BeNull();
        breedChild.Drives?[1].Location.Should().Be("peng");
    }

    [Fact]
    public void Drive_with_default_values_is_merged()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Drives =
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Size = 100,
                }
            ]
        };

        var child = new CatletConfig
        {
            Name = "child",
            Drives =
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.VHD,
                }
            ]
        };

        var breedChild = parent.Breed(child, "reference");

        breedChild.Drives.Should().SatisfyRespectively(
            breedDrive =>
            {
                breedDrive.Name.Should().Be("sda");
                breedDrive.Size.Should().Be(100);
                breedDrive.Source.Should().Be("gene:reference:sda");
            });
    }

    [Fact]
    public void NetworkAdapters_are_merged()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            NetworkAdapters = new[]
            {
                new CatletNetworkAdapterConfig()
                {
                    Name = "sda",
                    MacAddress = "addr1"
                }
            }
        };

        var child = new CatletConfig
        {
            Name = "child",
            NetworkAdapters = new[]
            {
                new CatletNetworkAdapterConfig()
                {
                    Name = "sda",
                    MacAddress = "addr2"
                }
            }
        };

        var breedChild = parent.Breed(child);

        breedChild.NetworkAdapters.Should().NotBeNull();
        breedChild.NetworkAdapters.Should().NotBeEquivalentTo(parent.NetworkAdapters);
        breedChild.NetworkAdapters.Should().HaveCount(1);
        breedChild.NetworkAdapters?[0].MacAddress.Should().Be("addr2");

    }

    [Fact]
    public void Networks_are_merged()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Networks = new[]
            {
                new CatletNetworkConfig()
                {
                    Name = "sda",
                    AdapterName = "eth2",
                    SubnetV4 = new CatletSubnetConfig
                    {
                        Name = "default",
                        IpPool = "other"
                    },
                    SubnetV6 = new CatletSubnetConfig
                    {
                        Name = "default",
                        IpPool = "default"
                    }
                }
            }
        };

        var child = new CatletConfig
        {
            Name = "child",
            Networks = new[]
            {
                new CatletNetworkConfig()
                {
                    Name = "sda",
                    AdapterName = "eth1",
                    SubnetV4 = new CatletSubnetConfig
                    {
                        Name = "none-default"
                    }
                }
            }
        };

        var breedChild = parent.Breed(child);

        breedChild.Networks.Should().NotBeNull();
        breedChild.Networks.Should().NotBeEquivalentTo(parent.Networks);
        breedChild.Networks.Should().HaveCount(1);
        breedChild.Networks?[0].AdapterName.Should().Be("eth1");
        breedChild.Networks?[0].SubnetV4.Should().NotBeNull();
        breedChild.Networks?[0].SubnetV4?.Name.Should().Be("none-default");
        breedChild.Networks?[0].SubnetV4?.IpPool.Should().BeNull();
        breedChild.Networks?[0].SubnetV6.Should().NotBeNull();
        breedChild.Networks?[0].SubnetV6?.Name.Should().Be("default");
        breedChild.Networks?[0].SubnetV6?.IpPool.Should().Be("default");

    }

    [Fact]
    public void Memory_is_merged()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Memory = new CatletMemoryConfig
            {
                Startup = 2048,
                Minimum = 1024,
                Maximum = 9096
            }
        };

        var child = new CatletConfig
        {
            Name = "child",
            Memory = new CatletMemoryConfig
            {
                Startup = 2049,
                Minimum = 1025,
                Maximum = 9097
            }
        };

        var breedChild = parent.Breed(child);

        breedChild.Memory.Should().NotBeNull();
        breedChild.Memory?.Startup.Should().Be(2049);
        breedChild.Memory?.Minimum.Should().Be(1025);
        breedChild.Memory?.Maximum.Should().Be(9097);
    }

    [Fact]
    public void Fodder_is_mixed()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Fodder = new[]
            {
                new FodderConfig()
                {
                    Name = "cfg",
                    Type = "type1",
                    Content = "contenta",
                    FileName = "filenamea"
                }
            }
        };

        var child = new CatletConfig
        {
            Name = "child",
            Fodder = new[]
            {
                new FodderConfig()
                {
                    Name = "cfg",
                    Type = "type2",
                    Content = "contentb",
                    FileName = "filenameb"
                }
            }
        };

        var breedChild = parent.Breed(child);

        breedChild.Fodder.Should().NotBeNull();
        breedChild.Fodder.Should().NotBeEquivalentTo(parent.Fodder);
        breedChild.Fodder.Should().HaveCount(1);
        breedChild.Fodder?[0].Type.Should().Be("type2");
        breedChild.Fodder?[0].Content.Should().Be("contentb");
        breedChild.Fodder?[0].FileName.Should().Be("filenameb");

    }

    [Fact]
    public void Fodder_is_removed()
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Fodder = new[]
            {
                new FodderConfig()
                {
                    Name = "cfg",
                    Type = "type1",
                    Content = "contenta",
                    FileName = "filenamea"
                }
            }
        };

        var child = new CatletConfig
        {
            Name = "child",
            Fodder = new[]
            {
                new FodderConfig()
                {
                    Name = "cfg",
                    Remove = true
                }
            }
        };

        var breedChild = parent.Breed(child);

        breedChild.Fodder.Should().NotBeNull();
        breedChild.Fodder.Should().HaveCount(0);

    }

    [Theory]
    [InlineData(null)]
    [InlineData("fodder")]
    public void Fodder_from_source_is_not_removed(string fodderName)
    {
        var parent = new CatletConfig
        {
            Name = "Parent",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = fodderName,
                    Source = "gene:somegene/utt/123:gene1",
                },
            ],
        };

        var child = new CatletConfig
        {
            Name = "child",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = fodderName,
                    Source = "gene:somegene/utt/123:gene1",
                    Remove = true,
                },
            ],
        };

        var breedChild = parent.Breed(child);

        breedChild.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be(fodderName);
                fodder.Source.Should().Be("gene:somegene/utt/123:gene1");
                fodder.Remove.Should().BeTrue();
            });
    }

    [Fact]
    public void Fodder_with_and_without_source_is_mixed_separately()
    {
        var parent = new CatletConfig
        {
            Name = "parent",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "fodder",
                    Content = "parent fodder content",
                },
                new FodderConfig()
                {
                    Source = "gene:somegene/utt/123:gene1",
                },
                new FodderConfig()
                {
                    Name = "fodder",
                    Source = "gene:somegene/utt/123:gene1",
                },
                new FodderConfig()
                {
                    Source = "gene:somegene/utt/123:gene2",
                },
            ],
        };

        var child = new CatletConfig
        {
            Name = "child",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "fodder",
                    Content = "child fodder content",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "testVariable",
                            Value = "child fodder value",
                        },
                    ],
                },
                new FodderConfig()
                {
                    Source = "gene:somegene/utt/123:gene1",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "testVariable",
                            Value = "gene 1 child fodder value",
                        },
                    ],
                },
                new FodderConfig()
                {
                    Name = "fodder",
                    Source = "gene:somegene/utt/123:gene1",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "testVariable",
                            Value = "gene 1 with name child fodder value",
                        },
                    ],
                },
                new FodderConfig()
                {
                    Source = "gene:somegene/utt/123:gene2",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "testVariable",
                            Value = "gene 2 child fodder value",
                        },
                    ],
                },
            ],
        };

        var breedChild = parent.Breed(child, "dbosoft/testparent");

        breedChild.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("fodder");
                fodder.Source.Should().Be("gene:dbosoft/testparent:catlet");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("testVariable");
                        variable.Value.Should().Be("child fodder value");
                    });
            },
            fodder =>
            {
                fodder.Name.Should().BeNull();
                fodder.Source.Should().Be("gene:somegene/utt/123:gene1");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("testVariable");
                        variable.Value.Should().Be("gene 1 child fodder value");
                    });
            },
            fodder =>
            {
                fodder.Name.Should().Be("fodder");
                fodder.Source.Should().Be("gene:somegene/utt/123:gene1");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("testVariable");
                        variable.Value.Should().Be("gene 1 with name child fodder value");
                    });
            },
            fodder =>
            {
                fodder.Name.Should().BeNull();
                fodder.Source.Should().Be("gene:somegene/utt/123:gene2");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("testVariable");
                        variable.Value.Should().Be("gene 2 child fodder value");
                    });
            });
    }

    [Theory]
    [InlineData(MutationType.Merge, 2)]
    [InlineData(MutationType.Remove, 1)]
    [InlineData(MutationType.Overwrite, 2)]
    public void Mutates(MutationType type, int expectedCount)
    {
        var parent = new CatletConfig
        {
            Capabilities = new[]
            {
                new CatletCapabilityConfig
                {
                    Name = "cap1",
                    Details = new[] { "any" }
                },
                new CatletCapabilityConfig
                {
                    Name = "cap2"
                },
                new CatletCapabilityConfig
                {
                    Name = "cap3",
                    Mutation = MutationType.Remove
                },
            }
        };

        var child = new CatletConfig
        {
            Capabilities = new[]
            {
                new CatletCapabilityConfig
                {
                    Name = "cap1",
                    Mutation = type,
                    Details = new[] { "none" }
                },
                new CatletCapabilityConfig
                {
                    Name = "cap2",
                },
            }
        };

        var breedChild = parent.Breed(child);

        breedChild.Capabilities.Should().NotBeNull();
        breedChild.Capabilities.Should().HaveCount(expectedCount);

        if (type == MutationType.Overwrite)
        {
            breedChild.Capabilities?[0].Details.Should().BeEquivalentTo(new[] { "none" });
        }
    }


    [Fact]
    public void Variables_are_not_mutated_but_child_replaces_parent()
    {
        var parent = new CatletConfig
        {
            Variables =
            [
                new VariableConfig
                {
                    Name = "catletVariable",
                    Type = VariableType.Number,
                    Value = "4.2",
                    Required = true,
                    Secret = true,
                },
            ],
        };

        var child = new CatletConfig
        {
            Variables =
            [
                new VariableConfig
                {
                    Name = "catletVariable",
                    Value = "string value",
                },
            ],
        };

        var breedChild = parent.Breed(child);

        breedChild.Variables.Should().SatisfyRespectively(
            variable =>
            {
                variable.Name.Should().Be("catletVariable");
                variable.Type.Should().BeNull();
                variable.Value.Should().Be("string value");
                variable.Required.Should().BeNull();
                variable.Secret.Should().BeNull();
            });
    }

    [Fact]
    public void Variables_in_fodder_are_replaced_when_content_is_mutated()
    {
        var parent = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Name = "fodder",
                    Content = "parent fodder content",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "parentVariable",
                            Type = VariableType.Number,
                            Value = "4.2",
                            Required = true,
                            Secret = true,
                        },
                    ],
                },
            ],
        };

        var child = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Name = "fodder",
                    Content = "child fodder content",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "childVariable",
                            Value = "string value",
                        },
                    ],
                },
            ],
        };

        var breedChild = parent.Breed(child);

        breedChild.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Content.Should().Be("child fodder content");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("childVariable");
                        variable.Type.Should().BeNull();
                        variable.Value.Should().Be("string value");
                        variable.Required.Should().BeNull();
                        variable.Secret.Should().BeNull();
                    });
            });
    }

    public static readonly IEnumerable<string> fodderNames =
    [
        "test-fodder",
        "TEST-FODDER"
    ];

    public static readonly IEnumerable<string> genesets =
    [
        "somegene/utt/123",
        "SOMEGENE/UTT/123",
    ];

    [Theory, CombinatorialData]
    public void Variables_in_fodder_are_replaced_when_source_and_name_are_specified(
        [CombinatorialMemberData(nameof(fodderNames))] string parentFodderName,
        [CombinatorialMemberData(nameof(genesets))] string parentGeneset,
        [CombinatorialMemberData(nameof(fodderNames))] string childFodderName,
        [CombinatorialMemberData(nameof(genesets))] string childGeneset)
    {
        var parent = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Name = parentFodderName,
                    Source = $"gene:{parentGeneset}:gene1",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "parentVariable",
                            Type = VariableType.Number,
                            Value = "4.2",
                            Required = true,
                            Secret = true,
                        },
                    ],
                },
            ],
        };

        var child = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Name = childFodderName,
                    Source = $"gene:{childGeneset}:gene1",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "childVariable",
                            Value = "string value",
                        },
                    ],
                },
            ],
        };

        var breedChild = parent.Breed(child);

        breedChild.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be(parentFodderName);
                fodder.Source.Should().Be($"gene:{parentGeneset}:gene1");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("childVariable");
                        variable.Type.Should().BeNull();
                        variable.Value.Should().Be("string value");
                        variable.Required.Should().BeNull();
                        variable.Secret.Should().BeNull();
                    });
            });
    }

    [Theory, CombinatorialData]
    public void Variables_in_fodder_are_replaced_when_source_is_specified(
    [CombinatorialMemberData(nameof(genesets))] string parentGeneset,
    [CombinatorialMemberData(nameof(genesets))] string childGeneset)
    {
        var parent = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{parentGeneset}:gene1",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "parentVariable",
                            Type = VariableType.Number,
                            Value = "4.2",
                            Required = true,
                            Secret = true,
                        },
                    ],
                },
            ],
        };

        var child = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{childGeneset}:gene1",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "childVariable",
                            Value = "string value",
                        },
                    ],
                },
            ],
        };

        var breedChild = parent.Breed(child);

        breedChild.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().BeNull();
                fodder.Source.Should().Be($"gene:{parentGeneset}:gene1");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("childVariable");
                        variable.Type.Should().BeNull();
                        variable.Value.Should().Be("string value");
                        variable.Required.Should().BeNull();
                        variable.Secret.Should().BeNull();
                    });
            });
    }

    [Fact]
    public void Variables_in_fodder_are_not_replaced_when_content_is_not_mutated()
    {
        var parent = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Name = "fodder",
                    Content = "parent fodder content",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "parentVariable",
                            Type = VariableType.Number,
                            Value = "4.2",
                            Required = true,
                            Secret = true,
                        },
                    ],
                },
            ],
        };

        var child = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Name = "fodder",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "childVariable",
                            Value = "string value",
                        },
                    ],
                },
            ],
        };

        var breedChild = parent.Breed(child);

        breedChild.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Content.Should().Be("parent fodder content");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("parentVariable");
                        variable.Type.Should().Be(VariableType.Number);
                        variable.Value.Should().Be("4.2");
                        variable.Required.Should().BeTrue();
                        variable.Secret.Should().BeTrue();
                    });
            });
    }
}
