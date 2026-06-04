using System.Collections.Generic;
using Eryph.GuestServices.Core;
using Eryph.GuestServices.HvDataExchange.Host;
using Moq;

namespace Eryph.Modules.HostAgent.HyperV.Test;

public class GuestStatusReaderTests
{
    private static readonly Guid VmId = Guid.Parse("2fe70974-c81a-4f3a-bf4e-7be405b88c97");

    private readonly Mock<IHostDataExchange> _hostDataExchange = new();

    private GuestStatusReader CreateSut() => new(_hostDataExchange.Object);

    private void SetupPools(Dictionary<string, string> guest, Dictionary<string, string> external)
    {
        _hostDataExchange.Setup(h => h.GetGuestDataAsync(VmId)).ReturnsAsync(guest);
        _hostDataExchange.Setup(h => h.GetExternalDataAsync(VmId)).ReturnsAsync(external);
    }

    [Fact]
    public async Task ReadAsync_MapsKnownKvpKeys()
    {
        SetupPools(
            new Dictionary<string, string>
            {
                [Constants.StatusKey] = "available",
                [Constants.VersionKey] = "0.4.0",
                ["eryph.provisioning.state"] = "completed",
            },
            new Dictionary<string, string>
            {
                [Constants.ShellKey] = "/bin/bash",
            });

        var status = await CreateSut().ReadAsync(VmId);

        status.GuestServicesStatus.Should().Be("available");
        status.GuestServicesVersion.Should().Be("0.4.0");
        status.ProvisioningState.Should().Be("completed");
        status.Shell.Should().Be("/bin/bash");
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullsForAbsentKeys()
    {
        SetupPools(new Dictionary<string, string>(), new Dictionary<string, string>());

        var status = await CreateSut().ReadAsync(VmId);

        status.GuestServicesStatus.Should().BeNull();
        status.GuestServicesVersion.Should().BeNull();
        status.ProvisioningState.Should().BeNull();
        status.Shell.Should().BeNull();
    }
}
