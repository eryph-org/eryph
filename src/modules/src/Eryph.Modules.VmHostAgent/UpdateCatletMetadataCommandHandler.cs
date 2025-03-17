using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent
{
    internal class UpdateCatletMetadataCommandHandler : 
        CatletConfigCommandHandler<UpdateCatletMetadataCommand, Unit>
    {
        public UpdateCatletMetadataCommandHandler(IPowershellEngine engine, ITaskMessaging messaging, ILogger log) : base(engine, messaging, log)
        {
        }

        protected override EitherAsync<Error, Unit> HandleCommand(
            UpdateCatletMetadataCommand command) =>
            from optionalVmInfo in GetVmInfo(command.VMId, Engine)
            from vmInfo in optionalVmInfo.ToEitherAsync(
                Error.New(Error.New($"The VM with ID {command.VMId} was not found.")))
            from currentMetadata in EnsureMetadata(vmInfo, command.CurrentMetadataId)
            from _ in SetMetadataId(vmInfo, command.NewMetadataId)
            select Unit.Default;
    }
}
