using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Events;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Newtonsoft.Json;
using Rebus;
using Rebus.Bus;
using Rebus.Transport;

namespace Haipa.Modules.VmHostAgent
{
    internal abstract class VirtualMachineConfigCommandHandler
    {
        protected readonly IPowershellEngine _engine;
        protected readonly IBus _bus;
        protected Guid _operationId;
        protected Guid _taskId;

        protected VirtualMachineConfigCommandHandler(
            IPowershellEngine engine,
            IBus bus)
        {
            _engine = engine;
            _bus = bus;
        }


        protected async Task<Unit> ProgressMessage(string message)
        {
            using (var scope = new RebusTransactionScope())
            {
                await _bus.Publish(new OperationTaskProgressEvent
                {
                    Id = Guid.NewGuid(),
                    OperationId = _operationId,
                    TaskId = _taskId,
                    Message = message,
                    Timestamp = DateTimeOffset.UtcNow,
                }).ConfigureAwait(false);

                // commit it like this
                await scope.CompleteAsync().ConfigureAwait(false);
            }
            return Unit.Default;

        }


        protected async Task<Unit> HandleError(PowershellFailure failure)
        {

            await _bus.Publish(OperationTaskStatusEvent.Failed(
                _operationId, _taskId,
                new ErrorData { ErrorMessage = failure.Message })
            ).ConfigureAwait(false);

            return Unit.Default;
        }

        protected static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> AttachToOperation(Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>> vmInfo, IBus bus, Guid operationId)
        {
            return vmInfo.MapAsync(async
                    info =>
                {
                    await bus.Send(new AttachMachineToOperationCommand
                    {
                        OperationId = operationId,
                        AgentName = Environment.MachineName,
                        MachineId = info.Value.Id,
                    }).ConfigureAwait(false);
                    return info;
                }

            );

        }

        protected Task<Either<PowershellFailure, string>> GenerateId()
        {
            return Prelude.TryAsync(() =>
                    _bus.SendRequest<GenerateIdReply>(new GenerateIdCommand(), null, TimeSpan.FromMinutes(5))
                        .Map(r => LongToString(r.GeneratedId, 36)))

                .ToEither(ex => new PowershellFailure { Message = ex.Message }).ToEither();

        }

        public const string DefaultDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static string LongToString(BigInteger subject, int @base, string digits = DefaultDigits)
        {
            if (@base < 2) { throw (new ArgumentException("Base must not be less than 2", nameof(@base))); }
            if (digits.Length < @base) { throw (new ArgumentException("Not enough Digits for the base", nameof(digits))); }


            var result = new StringBuilder();
            var sign = 1;

            if (subject < 0) { subject *= sign = -1; }

            do
            {
                result.Insert(0, digits[(int)(subject % @base)]);
                subject /= @base;
            }
            while (subject > 0);

            if (sign == -1)
            {
                result.Insert(0, '-');
            }

            return (result.ToString());
        }


        protected static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureNameConsistent(TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig config, IPowershellEngine engine)
        {
            return Prelude.Cond<(string currentName, string newName)>((names) =>
                    !string.IsNullOrWhiteSpace(names.newName) &&
                    !names.newName.Equals(names.currentName, StringComparison.InvariantCulture))((vmInfo.Value.Name,
                    config.Name))

                .MatchAsync(
                    None: () => vmInfo,
                    Some: (some) => Converge.RenameVirtualMachine(engine, vmInfo, config.Name));


        }

        protected static Task<Either<PowershellFailure, MachineInfo>> CreateMachineInventory(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            var inventory = new VirtualMachineInventory();
            return inventory.InventorizeVM(vmInfo).ToAsync().ToEither();
        }
    }
}