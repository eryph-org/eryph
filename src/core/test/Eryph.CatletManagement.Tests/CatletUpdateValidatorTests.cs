using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using FluentAssertions;
using FluentAssertions.LanguageExt;

namespace Eryph.CatletManagement.Tests;

public class CatletUpdateValidatorTests
{
    [Fact]
    public void Validate_ValidUpdate_ReturnsSuccess()
    {
        var currentConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Cpu = new CatletCpuConfig
            {
                Count = 2,
            },
            Memory = new CatletMemoryConfig
            {
                Startup = 1024,
            },
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:sda",
                    Store = "test-store",
                    Location = "test-location"
                }
            ],
        };

        var updateConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Cpu = new CatletCpuConfig
            {
                Count = 3,
            },
            Memory = new CatletMemoryConfig
            {
                Startup = 2048,
            },
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:sda",
                    Store = "test-store",
                    Location = "test-location"
                }
            ]
        };

        var result = CatletUpdateValidator.Validate(updateConfig, currentConfig);

        result.Should().BeSuccess();
    }

    [Fact]
    public void Validate_PropertiesChanged_ReturnsFail()
    {
        var currentConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host"
        };

        var updateConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Specification,
            Project = "other-project",
            Environment = "other-environment",
            Store = "other-store",
            Location = "other-location",
            Parent = "acme/acme-os/2.0",
            Hostname = "other-host"
        };

        var result = CatletUpdateValidator.Validate(updateConfig, currentConfig);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("ConfigType");
                issue.Message.Should().Be("The configuration must be an instance configuration.");
            },
            issue =>
            {
                issue.Member.Should().Be("Project");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Environment");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Store");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Location");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Parent");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Hostname");
                issue.Message.Should().Be("The hostname cannot be changed when updating an existing catlet.");
            });
    }

    [Fact]
    public void Validate_FodderAndVariablesSpecified_ReturnsFail()
    {
        var currentConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
        };

        var updateConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Fodder = 
            [
                new FodderConfig
                {
                    Name = "test-fodder",
                    Content = "test content"
                }
            ],
            Variables = 
            [
                new VariableConfig
                {
                    Name = "test_variable",
                    Value = "test-value"
                }
            ]
        };

        var result = CatletUpdateValidator.Validate(updateConfig, currentConfig);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Fodder");
                issue.Message.Should().Be("Fodder is not supported when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Variables");
                issue.Message.Should().Be("Variables are not supported when updating an existing catlet.");
            });
    }

    [Fact]
    public void Validate_GeneDriveAdded_ReturnsFail()
    {
        var currentConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:system"
                }
            ]
        };

        var updateConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:system"
                },
                new CatletDriveConfig
                {
                    Name = "sdb",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-tools/1.0:sdb"
                }
            ]
        };

        var result = CatletUpdateValidator.Validate(updateConfig, currentConfig);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Drives[1].Source");
                issue.Message.Should().Be("Cannot add new gene pool drives when updating an existing catlet.");
            });
    }

    [Fact]
    public void Validate_GeneDriveRemoved_ReturnsFail()
    {
        var currentConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:sda"
                },
            ]
        };

        var updateConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = null,
        };

        var result = CatletUpdateValidator.Validate(updateConfig, currentConfig);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Drives");
                issue.Message.Should().Be("The drive sda cannot be removed when updating the catlet.");
            });
    }

    [Fact]
    public void Validate_GeneDriveChanged_ReturnsFail()
    {
        var currentConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:sda",
                    Store = "test-store",
                    Location = "test-location"
                }
            ]
        };

        var updateConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Phd,
                    Source = "gene:acme/acme-os/2.0:sda",
                    Store = "other-store",
                    Location = "other-location"
                }
            ]
        };

        var result = CatletUpdateValidator.Validate(updateConfig, currentConfig);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Drives[0].Type");
                issue.Message.Should().Be("The drive type of a gene pool drive cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Drives[0].Store");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Drives[0].Location");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            },
            issue =>
            {
                issue.Member.Should().Be("Drives[0].Source");
                issue.Message.Should().Be("The value cannot be changed when updating an existing catlet.");
            });
    }

    [Fact]
    public void Validate_DriveWithoutGeneAdded_ReturnsSuccess()
    {
        var currentConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:sda"
                }
            ]
        };

        var updateConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Instance,
            Project = "test-project",
            Environment = "test-environment",
            Store = "test-store",
            Location = "test-location",
            Parent = "acme/acme-os/1.0",
            Hostname = "test-host",
            Drives = 
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Type = CatletDriveType.Vhd,
                    Source = "gene:acme/acme-os/1.0:sda"
                },
                new CatletDriveConfig
                {
                    Name = "sdb",
                    Type = CatletDriveType.Vhd,
                    Size = 1024
                }
            ]
        };

        var result = CatletUpdateValidator.Validate(updateConfig, currentConfig);

        result.Should().BeSuccess();
    }
}
