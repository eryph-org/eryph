using System.Collections.Generic;
using System.Threading;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.HostAgent.Channels;
using Moq;

namespace Eryph.Modules.HostAgent.HyperV.Test.Channels;

public class SshChannelCommandHandlerTests
{
    private static readonly Guid VmId = Guid.Parse("2fe70974-c81a-4f3a-bf4e-7be405b88c97");
    private static readonly Guid CatletId = Guid.Parse("de8c6710-172a-44be-bbed-27ba9905ed8f");
    private const string SubjectId = "client-123";
    private const string PublicKey = "ssh-ed25519 AAAAExampleKey operator@host";

    private readonly Mock<ITaskMessaging> _messaging = new();
    private readonly Mock<IChannelService> _channelService = new();

    [Fact]
    public async Task OpenSshChannel_RegistersChannelAndCompletesWithToken()
    {
        var expiry = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var expiresAt = new DateTimeOffset(2030, 1, 2, 3, 4, 20, TimeSpan.Zero);
        _channelService
            .Setup(c => c.RegisterChannel(VmId, SubjectId, PublicKey, expiry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelRegistration
            {
                Token = "the-token",
                AgentEndpoint = "wss://agent.test:9700/v1/channels/the-token",
                ExpiresAt = expiresAt,
            });

        var handler = new OpenSshChannelVMCommandHandler(_messaging.Object, _channelService.Object);
        await handler.Handle(CreateTask(new OpenSshChannelVMCommand
        {
            CatletId = CatletId,
            VmId = VmId,
            SubjectId = SubjectId,
            PublicKey = PublicKey,
            KeyExpiry = expiry,
        }));

        _messaging.Verify(
            m => m.CompleteTask(
                It.IsAny<IOperationTaskMessage>(),
                It.Is<object>(o =>
                    o is OpenSshChannelVMCommandResponse
                    && ((OpenSshChannelVMCommandResponse)o).Token == "the-token"
                    && ((OpenSshChannelVMCommandResponse)o).AgentEndpoint == "wss://agent.test:9700/v1/channels/the-token"
                    && ((OpenSshChannelVMCommandResponse)o).ExpiresAt == expiresAt),
                It.IsAny<IDictionary<string, string>?>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenSshChannel_WhenChannelServiceThrows_FailsTask()
    {
        _channelService
            .Setup(c => c.RegisterChannel(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var handler = new OpenSshChannelVMCommandHandler(_messaging.Object, _channelService.Object);
        await handler.Handle(CreateTask(new OpenSshChannelVMCommand
        {
            CatletId = CatletId,
            VmId = VmId,
            SubjectId = SubjectId,
        }));

        _messaging.Verify(
            m => m.FailTask(
                It.IsAny<IOperationTaskMessage>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>?>()),
            Times.Once);
        _messaging.Verify(
            m => m.CompleteTask(
                It.IsAny<IOperationTaskMessage>(),
                It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>?>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveSshKey_RemovesKeyAndCompletes()
    {
        _channelService
            .Setup(c => c.RemoveKey(VmId, SubjectId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RemoveSshKeyVMCommandHandler(_messaging.Object, _channelService.Object);
        await handler.Handle(CreateTask(new RemoveSshKeyVMCommand
        {
            CatletId = CatletId,
            VmId = VmId,
            SubjectId = SubjectId,
        }));

        _channelService.Verify(c => c.RemoveKey(VmId, SubjectId, It.IsAny<CancellationToken>()), Times.Once);
        _messaging.Verify(
            m => m.CompleteTask(It.IsAny<IOperationTaskMessage>(), It.IsAny<IDictionary<string, string>?>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenSshChannel_WithoutPublicKey_RegistersChannelWithoutKey()
    {
        _channelService
            .Setup(c => c.RegisterChannel(VmId, SubjectId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelRegistration
            {
                Token = "the-token",
                AgentEndpoint = "wss://agent.test:9700/v1/channels/the-token",
                ExpiresAt = default,
            });

        var handler = new OpenSshChannelVMCommandHandler(_messaging.Object, _channelService.Object);
        await handler.Handle(CreateTask(new OpenSshChannelVMCommand
        {
            CatletId = CatletId,
            VmId = VmId,
            SubjectId = SubjectId,
            PublicKey = null,
            KeyExpiry = null,
        }));

        _channelService.Verify(
            c => c.RegisterChannel(VmId, SubjectId, null, null, It.IsAny<CancellationToken>()), Times.Once);
        _messaging.Verify(
            m => m.CompleteTask(
                It.IsAny<IOperationTaskMessage>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>?>()),
            Times.Once);
    }

    [Fact]
    public async Task AddSshKey_AddsKeyAndCompletes()
    {
        var expiry = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        _channelService
            .Setup(c => c.AddKey(VmId, SubjectId, PublicKey, expiry, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AddSshKeyVMCommandHandler(_messaging.Object, _channelService.Object);
        await handler.Handle(CreateTask(new AddSshKeyVMCommand
        {
            CatletId = CatletId,
            VmId = VmId,
            SubjectId = SubjectId,
            PublicKey = PublicKey,
            KeyExpiry = expiry,
        }));

        _channelService.Verify(
            c => c.AddKey(VmId, SubjectId, PublicKey, expiry, It.IsAny<CancellationToken>()), Times.Once);
        _messaging.Verify(
            m => m.CompleteTask(It.IsAny<IOperationTaskMessage>(), It.IsAny<IDictionary<string, string>?>()),
            Times.Once);
    }

    private static OperationTask<T> CreateTask<T>(T command) where T : class, new() =>
        new(command, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
}
