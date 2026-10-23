using System.Text;
using Dbosoft.CloudInit.ConfigDrive;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Genetics;

public class CatletFodderUserDataTests
{
    private static readonly Guid CatletId = Guid.NewGuid();
    private static readonly Guid VmId = Guid.NewGuid();

    [Theory]
    [InlineData("cloud-config", "text/cloud-config")]
    [InlineData("shellscript", "text/x-shellscript")]
    [InlineData("cloud-boothook", "text/cloud-boothook")]
    [InlineData("include-url", "text/x-include-url")]
    [InlineData(null, "text/cloud-config")]
    [InlineData("unknown", "text/cloud-config")]
    public void CreateUserData_FodderType_IsMappedToContentType(
        string? fodderType,
        string expectedContentType)
    {
        var result = CatletFodderUserData.CreateUserData(
            [new FodderConfig { Name = "test", Type = fodderType, Content = "content" }],
            withoutSecrets: false);

        result.Should().SatisfyRespectively(
            userData => userData.ContentType.Name.Should().Be(expectedContentType));
    }

    [Fact]
    public void CreateUserData_Fodder_ContentAndFilenameAreMapped()
    {
        var result = CatletFodderUserData.CreateUserData(
            [
                new FodderConfig { Name = "first", Content = "first content\0\0", Filename = "first.yml" },
                new FodderConfig { Name = "second", Content = null },
            ],
            withoutSecrets: false);

        result.Should().SatisfyRespectively(
            userData =>
            {
                userData.Content.Should().Be("first content");
                userData.FileName.Should().Be("first.yml");
                userData.Encoding.Should().Be(Encoding.UTF8);
            },
            userData =>
            {
                userData.Content.Should().BeEmpty();
                userData.FileName.Should().BeNull();
            });
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void CreateUserData_SecretFodder_IsOnlySkippedWithoutSecrets(
        bool withoutSecrets,
        int expectedCount)
    {
        var result = CatletFodderUserData.CreateUserData(
            [
                new FodderConfig { Name = "public", Content = "public" },
                new FodderConfig { Name = "secret", Content = "secret", Secret = true },
            ],
            withoutSecrets);

        result.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void CreateConfigDriveUserData_Fodder_IsMappedUnchanged()
    {
        var result = CatletFodderUserData.CreateConfigDriveUserData(
            [new FodderConfig { Name = "test", Type = "shellscript", Content = "#!/bin/sh" }],
            withoutSecrets: false);

        result.Should().SatisfyRespectively(
            userData =>
            {
                userData.ContentType.Should().Be(UserDataContentType.ShellScript);
                userData.Content.Should().Be("#!/bin/sh");
            });
    }

    [Fact]
    public void CreateConfigDriveUserData_NullFodder_ReturnsEmptyCloudConfig()
    {
        // cloud-init treats user-data as a required part of the NoCloud datasource,
        // so the config drive must always carry a user-data part.
        AssertEmptyCloudConfig(CatletFodderUserData.CreateConfigDriveUserData(
            null,
            withoutSecrets: false));
    }

    [Fact]
    public void CreateConfigDriveUserData_EmptyFodder_ReturnsEmptyCloudConfig()
    {
        AssertEmptyCloudConfig(CatletFodderUserData.CreateConfigDriveUserData(
            [],
            withoutSecrets: false));
    }

    [Fact]
    public void CreateConfigDriveUserData_OnlySecretFodderHidden_ReturnsEmptyCloudConfig()
    {
        AssertEmptyCloudConfig(CatletFodderUserData.CreateConfigDriveUserData(
            [new FodderConfig { Name = "secret", Content = "secret", Secret = true }],
            withoutSecrets: true));
    }

    private static void AssertEmptyCloudConfig(Seq<UserData> userData) =>
        userData.Should().SatisfyRespectively(
            data =>
            {
                data.ContentType.Should().Be(UserDataContentType.CloudConfig);
                data.Content.Should().Be("#cloud-config");
            });

    [Fact]
    public void CreateUserData_Config_VariablesAreSubstituted()
    {
        var config = new CatletConfig
        {
            Variables =
            [
                new VariableConfig { Name = "userName", Value = "alice" },
                new VariableConfig { Name = "password", Value = "s3cret", Secret = true },
            ],
            Fodder =
            [
                new FodderConfig
                {
                    Name = "user",
                    Type = "shellscript",
                    Content = "user={{ userName }} catlet={{ catletId }} vm={{ vmId }} greeting={{ greeting }}",
                    Variables =
                    [
                        new VariableConfig { Name = "greeting", Value = "hello {{ userName }}" },
                    ],
                },
                new FodderConfig
                {
                    Name = "password",
                    Content = "password={{ password }}",
                },
            ],
        };

        var result = CatletFodderUserData.CreateUserData(config, CatletId, VmId);

        result.Should().BeSuccess().Subject.Should().SatisfyRespectively(
            userData =>
            {
                userData.ContentType.Should().Be(UserDataContentType.ShellScript);
                userData.Content.Should().Be(
                    $"user=alice catlet={CatletId} vm={VmId} greeting=hello alice");
            },
            // Fodder which uses secret variables is included with the plain value.
            userData => userData.Content.Should().Be("password=s3cret"));
    }

    [Fact]
    public void CreateUserData_UndefinedVariable_Fails()
    {
        var config = new CatletConfig
        {
            Fodder = [new FodderConfig { Name = "test", Content = "{{ undefined }}" }],
        };

        var result = CatletFodderUserData.CreateUserData(config, CatletId, VmId);

        result.Should().BeFail();
    }

    [Fact]
    public async Task Serialize_UserData_IsPlainTextWithBase64EncodedParts()
    {
        var userData = Seq(
            new UserData(UserDataContentType.CloudConfig, "#cloud-config\nhostname: test", Encoding.UTF8)
            {
                FileName = "config.yml",
            },
            new UserData(UserDataContentType.ShellScript, "#!/bin/sh\necho test", Encoding.UTF8));

        var result = await CatletFodderUserData.Serialize(userData);

        result.Should().StartWith("From nobody");
        result.Should().Contain("Content-Type: multipart/mixed");
        result.Should().Contain("Content-Type: text/cloud-config");
        result.Should().Contain("Content-Disposition: attachment; filename=\"config.yml\"");
        result.Should().Contain("Content-Type: text/x-shellscript");
        result.Should().Contain("Content-Transfer-Encoding: base64");
        result.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("#cloud-config\nhostname: test")));
        result.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("#!/bin/sh\necho test")));
        result.Should().NotContain("hostname: test");
    }
}
