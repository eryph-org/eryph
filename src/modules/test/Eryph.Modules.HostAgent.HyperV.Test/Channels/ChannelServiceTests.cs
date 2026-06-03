using System.Collections.Generic;
using System.IO;
using System.Threading;
using Eryph.GuestServices.Core;
using Eryph.GuestServices.HvDataExchange.Host;
using Eryph.Modules.HostAgent.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Eryph.Modules.HostAgent.HyperV.Test.Channels;

public class ChannelServiceTests
{
    private static readonly Guid VmId = Guid.Parse("2fe70974-c81a-4f3a-bf4e-7be405b88c97");
    private const string SubjectId = "client-123";
    private const string PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOperatorKey operator@host";

    private readonly Mock<IHostDataExchange> _hostDataExchange = new();
    private readonly Mock<IChannelEndpointProvider> _endpointProvider = new();

    public ChannelServiceTests()
    {
        _hostDataExchange
            .Setup(h => h.SetExternalValuesAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Returns(Task.CompletedTask);
        _endpointProvider
            .Setup(p => p.BuildChannelUrl(It.IsAny<string>()))
            .Returns<string>(token => $"wss://agent.test:9700/v1/channels/{token}");
    }

    private ChannelService CreateSut() =>
        new(_hostDataExchange.Object, _endpointProvider.Object, NullLogger<ChannelService>.Instance);

    [Fact]
    public async Task RegisterChannel_WithPublicKeyAndExpiry_WritesAuthorizedKeyLineWithCompactExpiry()
    {
        var written = CaptureKvpWrite();

        // A non-UTC offset must still be serialized as the OpenSSH compact UTC form yyyyMMddHHmmssZ;
        // the guest's ClientKeyProvider rejects any other form and treats it as expired.
        var expiry = new DateTimeOffset(2030, 1, 2, 4, 4, 5, TimeSpan.FromHours(1));
        await CreateSut().RegisterChannel(VmId, SubjectId, PublicKey, expiry);

        var slot = Constants.ClientAuthKeyPrefix + SubjectId;
        written.Value.Should().ContainKey(slot);
        written.Value[slot].Should().Be($"expiry-time=\"20300102030405Z\" {PublicKey}");
    }

    [Fact]
    public async Task RegisterChannel_WithPublicKeyNoExpiry_WritesBareKey()
    {
        var written = CaptureKvpWrite();

        await CreateSut().RegisterChannel(VmId, SubjectId, PublicKey, keyExpiry: null);

        var slot = Constants.ClientAuthKeyPrefix + SubjectId;
        written.Value[slot].Should().Be(PublicKey);
    }

    [Fact]
    public async Task RegisterChannel_TrimsSurroundingWhitespaceFromKey()
    {
        var written = CaptureKvpWrite();

        await CreateSut().RegisterChannel(VmId, SubjectId, $"  {PublicKey}\n", keyExpiry: null);

        var slot = Constants.ClientAuthKeyPrefix + SubjectId;
        written.Value[slot].Should().Be(PublicKey);
    }

    [Fact]
    public async Task RegisterChannel_WithoutPublicKey_DoesNotWriteKvp()
    {
        var registration = await CreateSut().RegisterChannel(VmId, SubjectId, publicKey: null, keyExpiry: null);

        _hostDataExchange.Verify(
            h => h.SetExternalValuesAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<string, string?>>()),
            Times.Never);
        registration.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterChannel_ReturnsTokenEndpointAndFutureExpiry()
    {
        var before = DateTimeOffset.UtcNow;

        var registration = await CreateSut().RegisterChannel(VmId, SubjectId, publicKey: null, keyExpiry: null);

        registration.AgentEndpoint.Should().Be($"wss://agent.test:9700/v1/channels/{registration.Token}");
        registration.ExpiresAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task RegisterChannel_MintsUniqueUrlSafeTokens()
    {
        var sut = CreateSut();

        var first = await sut.RegisterChannel(VmId, SubjectId, null, null);
        var second = await sut.RegisterChannel(VmId, SubjectId, null, null);

        first.Token.Should().NotBe(second.Token);
        // The token rides in the URL path, so it must not contain the non-URL-safe base64 characters.
        first.Token.Should().NotContainAny("+", "/", "=");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterChannel_WithoutSubjectId_Throws(string subjectId)
    {
        var act = () => CreateSut().RegisterChannel(VmId, subjectId, PublicKey, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveKey_WithoutSubjectId_Throws(string subjectId)
    {
        var act = () => CreateSut().RemoveKey(VmId, subjectId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RemoveKey_WritesNullValueToTheSubjectSlot()
    {
        var written = CaptureKvpWrite();

        await CreateSut().RemoveKey(VmId, SubjectId);

        var slot = Constants.ClientAuthKeyPrefix + SubjectId;
        written.Value.Should().ContainKey(slot);
        // HostDataExchange maps a null value to a remove-item call; the slot must match what was written.
        written.Value[slot].Should().BeNull();
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
        var sut = new RecordingChannelService(_hostDataExchange.Object, _endpointProvider.Object);
        var registration = await sut.RegisterChannel(VmId, SubjectId, null, null);

        var first = await sut.OpenChannelAsync(registration.Token);
        var second = await sut.OpenChannelAsync(registration.Token);

        first.Should().NotBeNull();
        second.Should().BeNull("the one-time token must not open a second channel");
        sut.ConnectCount.Should().Be(1);
    }

    [Fact]
    public async Task AddKey_WritesAuthorizedKeyLineWithCompactExpiry()
    {
        var written = CaptureKvpWrite();

        var expiry = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await CreateSut().AddKey(VmId, SubjectId, PublicKey, expiry);

        var slot = Constants.ClientAuthKeyPrefix + SubjectId;
        written.Value[slot].Should().Be($"expiry-time=\"20300102030405Z\" {PublicKey}");
    }

    [Fact]
    public async Task AddKey_WithoutExpiry_WritesBareKey()
    {
        var written = CaptureKvpWrite();

        await CreateSut().AddKey(VmId, SubjectId, PublicKey, keyExpiry: null);

        var slot = Constants.ClientAuthKeyPrefix + SubjectId;
        written.Value[slot].Should().Be(PublicKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddKey_WithoutPublicKey_Throws(string publicKey)
    {
        var act = () => CreateSut().AddKey(VmId, SubjectId, publicKey, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddKey_WithoutSubjectId_Throws(string subjectId)
    {
        var act = () => CreateSut().AddKey(VmId, subjectId, PublicKey, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private Ref<IReadOnlyDictionary<string, string?>> CaptureKvpWrite()
    {
        var captured = new Ref<IReadOnlyDictionary<string, string?>>();
        _hostDataExchange
            .Setup(h => h.SetExternalValuesAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Callback<Guid, IReadOnlyDictionary<string, string?>>((_, values) => captured.Value = values)
            .Returns(Task.CompletedTask);
        return captured;
    }

    private sealed class Ref<T>
    {
        public T Value { get; set; } = default!;
    }

    // Avoids dialing a real guest hvsocket so the token-consumption path is exercisable.
    private sealed class RecordingChannelService(
        IHostDataExchange hostDataExchange,
        IChannelEndpointProvider endpointProvider)
        : ChannelService(hostDataExchange, endpointProvider, NullLogger<ChannelService>.Instance)
    {
        public int ConnectCount { get; private set; }

        protected override Task<Stream> ConnectGuestAsync(Guid vmId, CancellationToken cancellationToken)
        {
            ConnectCount++;
            return Task.FromResult<Stream>(new MemoryStream());
        }
    }
}
