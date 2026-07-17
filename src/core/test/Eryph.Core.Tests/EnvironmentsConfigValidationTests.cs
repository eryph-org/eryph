using Eryph.Core.VmAgent;

namespace Eryph.Core.Tests;

public class EnvironmentsConfigValidationTests
{
    [Fact]
    public void Default_environment_is_always_allowed()
    {
        var distributed = new EnvironmentsConfig();

        EnvironmentsConfigValidation.IsEnvironmentAllowed(distributed, "default").Should().BeTrue();
    }

    [Theory]
    [InlineData("staging", true)]
    [InlineData("STAGING", true)] // case-insensitive
    [InlineData("prod", false)]
    public void Environment_is_allowed_only_when_in_the_distributed_vocabulary(string name, bool expected)
    {
        var distributed = new EnvironmentsConfig
        {
            Environments = [new EnvironmentConfig { Name = "staging", Site = "default" }],
        };

        EnvironmentsConfigValidation.IsEnvironmentAllowed(distributed, name).Should().Be(expected);
    }

    [Fact]
    public void Default_environment_resolves_to_the_default_site_without_configuration()
    {
        var distributed = new EnvironmentsConfig();

        EnvironmentsConfigValidation.FindSite(distributed, "default")
            .Should().Be(EryphConstants.DefaultSiteName);
    }

    [Fact]
    public void Site_is_found_for_a_configured_environment()
    {
        var distributed = new EnvironmentsConfig
        {
            Environments = [new EnvironmentConfig { Name = "staging", Site = "berlin" }],
        };

        EnvironmentsConfigValidation.FindSite(distributed, "staging").Should().Be("berlin");
    }

    [Fact]
    public void Site_is_null_for_an_unknown_environment()
    {
        var distributed = new EnvironmentsConfig
        {
            Environments = [new EnvironmentConfig { Name = "staging", Site = "berlin" }],
        };

        EnvironmentsConfigValidation.FindSite(distributed, "prod").Should().BeNull();
    }

    [Fact]
    public void Unused_local_environments_lists_names_not_in_the_distributed_vocabulary()
    {
        var distributed = new EnvironmentsConfig
        {
            Environments = [new EnvironmentConfig { Name = "staging", Site = "default" }],
        };
        var local = new VmHostAgentConfiguration
        {
            Environments =
            [
                new VmHostAgentEnvironmentConfiguration { Name = "staging" }, // distributed → used
                new VmHostAgentEnvironmentConfiguration { Name = "prod" }, // not distributed → unused
            ],
        };

        EnvironmentsConfigValidation.GetUnusedLocalEnvironments(distributed, local)
            .Should().BeEquivalentTo("prod");
    }

    [Fact]
    public void Validate_accepts_a_well_formed_config()
    {
        var config = new EnvironmentsConfig
        {
            Sites = [new SiteConfig { Name = "berlin" }],
            Environments = [new EnvironmentConfig { Name = "staging", Site = "berlin" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_an_environment_in_the_reserved_default_site()
    {
        // The default site always exists and is never declared.
        var config = new EnvironmentsConfig
        {
            Environments = [new EnvironmentConfig { Name = "staging", Site = "default" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should().BeEmpty();
    }

    [Fact]
    public void Validate_rejects_an_environment_whose_site_is_not_declared()
    {
        // The whole reason sites and environments are authored together: this would otherwise be
        // accepted, distributed, and then fail at every deployment into the environment.
        var config = new EnvironmentsConfig
        {
            Sites = [new SiteConfig { Name = "berlin" }],
            Environments = [new EnvironmentConfig { Name = "staging", Site = "munich" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should()
            .ContainSingle().Which.Should().Contain("'munich', which is not declared");
    }

    [Fact]
    public void Validate_rejects_the_reserved_default_site()
    {
        var config = new EnvironmentsConfig
        {
            Sites = [new SiteConfig { Name = "default" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should()
            .ContainSingle().Which.Should().Contain("reserved site name");
    }

    [Fact]
    public void Validate_rejects_duplicate_sites()
    {
        var config = new EnvironmentsConfig
        {
            Sites = [new SiteConfig { Name = "berlin" }, new SiteConfig { Name = "berlin" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should()
            .ContainSingle().Which.Should().Contain("site name 'berlin' is not unique");
    }

    [Fact]
    public void Validate_rejects_the_reserved_default_environment()
    {
        var config = new EnvironmentsConfig
        {
            Sites = [new SiteConfig { Name = "berlin" }],
            Environments = [new EnvironmentConfig { Name = "default", Site = "berlin" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should()
            .ContainSingle().Which.Should().Contain("reserved environment name");
    }

    [Fact]
    public void Validate_rejects_an_invalid_environment_name()
    {
        var config = new EnvironmentsConfig
        {
            Sites = [new SiteConfig { Name = "berlin" }],
            Environments = [new EnvironmentConfig { Name = "invalid name!", Site = "berlin" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should()
            .ContainSingle().Which.Should().Contain("invalid name!");
    }

    [Fact]
    public void Validate_rejects_a_missing_site()
    {
        var config = new EnvironmentsConfig
        {
            Environments = [new EnvironmentConfig { Name = "staging", Site = "" }],
        };

        EnvironmentsConfigValidation.Validate(config).Should()
            .ContainSingle().Which.Should().Contain("must not be empty");
    }

    [Fact]
    public void Validate_rejects_duplicate_environments()
    {
        var config = new EnvironmentsConfig
        {
            Sites = [new SiteConfig { Name = "berlin" }, new SiteConfig { Name = "munich" }],
            Environments =
            [
                new EnvironmentConfig { Name = "staging", Site = "berlin" },
                new EnvironmentConfig { Name = "staging", Site = "munich" },
            ],
        };

        EnvironmentsConfigValidation.Validate(config).Should()
            .ContainSingle().Which.Should().Contain("environment name 'staging' is not unique");
    }
}
