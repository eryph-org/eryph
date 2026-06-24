using Eryph.Modules.HostAgent.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Eryph.Modules.HostAgent.HyperV.Test.Channels;

public class ChannelServiceTests
{
    private const string Slot = "eryph:guest-services:client-public-key:client-123";
    private const string KeyLine = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOperatorKey operator@host";
    private static readonly Guid VmId = Guid.Parse("2fe70974-c81a-4f3a-bf4e-7be405b88c97");
    private readonly Mock<IChannelEndpointProvider> _endpointProvider = new();

    private readonly Mock<IGuestDataWriter> _writer = new();

    public ChannelServiceTests()
    {
        _writer
            .Setup(w => w.SetExternalAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Returns(Task.CompletedTask);
        _endpointProvider
            .Setup(p => p.BuildChannelUrl(It.IsAny<string>()))
            .Returns<string>(token => $"wss://agent.test:9700/v1/channels/{token}");
    }

    private ChannelService CreateSut() =>
        new(_writer.Object, _endpointProvider.Object, NullLogger<ChannelService>.Instance);

    [Fact]
    public async Task RegisterChannel_WithAccessKeyValues_WritesThemToTheExternalPool()
    {
        IReadOnlyDictionary<string, string?>? captured = null;
        _writer
            .Setup(w => w.SetExternalAsync(VmId, It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Callback<Guid, IReadOnlyDictionary<string, string?>>((_, values) => captured = values)
            .Returns(Task.CompletedTask);

        await CreateSut().RegisterChannel(VmId, new Dictionary<string, string> { [Slot] = KeyLine });

        captured.Should().NotBeNull();
        captured![Slot].Should().Be(KeyLine);
    }

    [Fact]
    public async Task RegisterChannel_WithoutAccessKeyValues_DoesNotWrite()
    {
        var registration = await CreateSut().RegisterChannel(VmId, new Dictionary<string, string>());

        _writer.Verify(
            w => w.SetExternalAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<string, string?>>()),
            Times.Never);
        registration.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterChannel_ReturnsTokenEndpointAndFutureExpiry()
    {
        var before = DateTimeOffset.UtcNow;

        var registration = await CreateSut().RegisterChannel(VmId, new Dictionary<string, string>());

        registration.AgentEndpoint.Should().Be($"wss://agent.test:9700/v1/channels/{registration.Token}");
        registration.ExpiresAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task RegisterChannel_MintsUniqueUrlSafeTokens()
    {
        var sut = CreateSut();

        var first = await sut.RegisterChannel(VmId, new Dictionary<string, string>());
        var second = await sut.RegisterChannel(VmId, new Dictionary<string, string>());

        first.Token.Should().NotBe(second.Token);
        // The token rides in the URL path, so it must not contain the non-URL-safe base64 characters.
        first.Token.Should().NotContainAny("+", "/", "=");
    }

    [Fact]
    public async Task OpenChannelAsync_UnknownToken_ReturnsNull()
    {
        var stream = await CreateSut().OpenChannelAsync("not-a-real-token");

        stream.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task OpenChannelAsync_EmptyToken_ReturnsNull(string? token)
    {
        var stream = await CreateSut().OpenChannelAsync(token!);

        stream.Should().BeNull();
    }

    [Fact]
    public async Task OpenChannelAsync_ConsumesTokenExactlyOnce()
    {
        var sut = new RecordingChannelService(_writer.Object, _endpointProvider.Object);
        var registration = await sut.RegisterChannel(VmId, new Dictionary<string, string>());

        var first = await sut.OpenChannelAsync(registration.Token);
        var second = await sut.OpenChannelAsync(registration.Token);

        first.Should().NotBeNull();
        second.Should().BeNull("the one-time token must not open a second channel");
        sut.ConnectCount.Should().Be(1);
    }

    // Avoids dialing a real guest hvsocket so the token-consumption path is exercisable.
    private sealed class RecordingChannelService(
        IGuestDataWriter guestDataWriter,
        IChannelEndpointProvider endpointProvider)
        : ChannelService(guestDataWriter, endpointProvider, NullLogger<ChannelService>.Instance)
    {
        public int ConnectCount { get; private set; }

        protected override Task<Stream> ConnectGuestAsync(Guid vmId, CancellationToken cancellationToken)
        {
            ConnectCount++;
            return Task.FromResult<Stream>(new MemoryStream());
        }
    }
}
