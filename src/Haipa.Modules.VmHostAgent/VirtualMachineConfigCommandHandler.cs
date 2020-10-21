using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Events;
using Haipa.Messages.Operations;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using LanguageExt;
using Rebus.Bus;
using Rebus.Transport;

namespace Haipa.Modules.VmHostAgent
{
    internal abstract class VirtualMachineConfigCommandHandler
    {
        protected readonly IPowershellEngine Engine;
        protected readonly IBus Bus;
        protected Guid OperationId;
        protected Guid TaskId;

        protected VirtualMachineConfigCommandHandler(
            IPowershellEngine engine,
            IBus bus)
        {
            Engine = engine;
            Bus = bus;
        }


        protected async Task<Unit> ProgressMessage(string message)
        {
            using (var scope = new RebusTransactionScope())
            {
                await Bus.Publish(new OperationTaskProgressEvent
                {
                    Id = Guid.NewGuid(),
                    OperationId = OperationId,
                    TaskId = TaskId,
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

            await Bus.Publish(OperationTaskStatusEvent.Failed(
                OperationId, TaskId,
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

        public const string DefaultDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static string LongToString(BigInteger subject, int @base = 36, string digits = DefaultDigits)
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
                    Some: (some) => VirtualMachine.Rename(engine, vmInfo, config.Name));


        }

        protected static Task<Either<PowershellFailure, MachineInfo>> CreateMachineInventory(
            IPowershellEngine engine, HostSettings hostSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            var inventory = new VirtualMachineInventory(engine, hostSettings);
            return inventory.InventorizeVM(vmInfo).ToAsync().ToEither();
        }
    }
}