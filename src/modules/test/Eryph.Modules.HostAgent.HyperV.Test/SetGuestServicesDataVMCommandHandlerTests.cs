using System.Collections.Generic;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Moq;

namespace Eryph.Modules.HostAgent.HyperV.Test;

public class SetGuestServicesDataVMCommandHandlerTests
{
    private static readonly Guid VmId = Guid.Parse("2fe70974-c81a-4f3a-bf4e-7be405b88c97");

    private readonly Mock<ITaskMessaging> _messaging = new();
    private readonly Mock<IGuestDataWriter> _writer = new();

    [Fact]
    public async Task Handle_WritesValuesAndDeletesRemoveKeys()
    {
        IReadOnlyDictionary<string, string?>? captured = null;
        _writer
            .Setup(w => w.SetExternalAsync(VmId, It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Callback<Guid, IReadOnlyDictionary<string, string?>>((_, values) => captured = values)
            .Returns(Task.CompletedTask);

        var handler = new SetGuestServicesDataVMCommandHandler(_messaging.Object, _writer.Object);
        await handler.Handle(CreateTask(new SetGuestServicesDataVMCommand
        {
            CatletId = Guid.NewGuid(),
            VmId = VmId,
            Values = new Dictionary<string, string> { ["eryph:guest-services:shell"] = "/bin/bash" },
            RemoveKeys = new List<string> { "eryph:guest-services:shell-args" },
        }));

        captured.Should().NotBeNull();
        captured!["eryph:guest-services:shell"].Should().Be("/bin/bash");
        captured.ContainsKey("eryph:guest-services:shell-args").Should().BeTrue();
        captured["eryph:guest-services:shell-args"].Should().BeNull();

        _messaging.Verify(
            m => m.CompleteTask(It.IsAny<IOperationTaskMessage>(), It.IsAny<IDictionary<string, string>?>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithRemoveKeysOnly_WritesKeysMappedToNull()
    {
        IReadOnlyDictionary<string, string?>? captured = null;
        _writer
            .Setup(w => w.SetExternalAsync(VmId, It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Callback<Guid, IReadOnlyDictionary<string, string?>>((_, values) => captured = values)
            .Returns(Task.CompletedTask);

        var handler = new SetGuestServicesDataVMCommandHandler(_messaging.Object, _writer.Object);
        await handler.Handle(CreateTask(new SetGuestServicesDataVMCommand
        {
            CatletId = Guid.NewGuid(),
            VmId = VmId,
            // Values defaults to an empty collection; this is the remove-keys-only path.
            RemoveKeys = new List<string> { "eryph:guest-services:shell", "eryph:guest-services:shell-args" },
        }));

        captured.Should().NotBeNull();
        captured!.ContainsKey("eryph:guest-services:shell").Should().BeTrue();
        captured["eryph:guest-services:shell"].Should().BeNull();
        captured.ContainsKey("eryph:guest-services:shell-args").Should().BeTrue();
        captured["eryph:guest-services:shell-args"].Should().BeNull();

        _messaging.Verify(
            m => m.CompleteTask(It.IsAny<IOperationTaskMessage>(), It.IsAny<IDictionary<string, string>?>()),
            Times.Once);
    }

    private static OperationTask<T> CreateTask<T>(T command) where T : class, new() =>
        new(command, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
}
