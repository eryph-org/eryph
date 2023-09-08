using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    internal abstract class CatletOperationHandlerBase<T> : IHandleMessages<OperationTask<T>>
        where T : class, IVMCommand, new()
    {
        private readonly ITaskMessaging _messaging;
        private readonly IPowershellEngine _engine;

        protected CatletOperationHandlerBase(ITaskMessaging messaging, IPowershellEngine engine)
        {
            _messaging = messaging;
            _engine = engine;
        }

        public Task Handle(OperationTask<T> message)
        {
            var command = message.Command;

            return GetVmInfo(command.VMId, _engine).ToError()
                .BindAsync(optVmInfo =>
                {
                    return optVmInfo.MatchAsync(
                        Some: s => HandleCommand(s, command, _engine),
                        None: () => Unit.Default);
                })
                .ToAsync()
                .FailOrComplete(_messaging, message);

        }

        protected abstract Task<Either<Error, Unit>> HandleCommand(
            TypedPsObject<VirtualMachineInfo> vmInfo, T command, IPowershellEngine engine);


        private Task<Either<PowershellFailure, Option<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(Guid vmId,
            IPowershellEngine engine)
        {
            return engine.GetObjectsAsync<VirtualMachineInfo>(CreateGetVMCommand(vmId))
                .MapAsync(seq => seq.HeadOrNone());
        }

        protected virtual PsCommandBuilder CreateGetVMCommand(Guid vmId)
        {
            return PsCommandBuilder.Create()
                .AddCommand("get-vm").AddParameter("Id", vmId);
        }
    }
}