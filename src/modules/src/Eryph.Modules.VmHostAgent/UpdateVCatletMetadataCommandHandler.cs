using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    internal class UpdateVCatletMetadataCommandHandler : 
        VirtualCatletConfigCommandHandler<UpdateVCatletMetadataCommand, Unit>
    {
        public UpdateVCatletMetadataCommandHandler(IPowershellEngine engine, ITaskMessaging messaging, ILogger log) : base(engine, messaging, log)
        {
        }

        protected override EitherAsync<Error, Unit> HandleCommand(UpdateVCatletMetadataCommand command)
        {

            var metadata = new VirtualCatletMetadata { Id = command.CurrentMetadataId };

            return
                from vmList in GetVmInfo(command.VMId, Engine)
                from vmInfo in EnsureSingleEntry(vmList, command.VMId)
                from currentMetadata in EnsureMetadata(metadata, vmInfo).ToAsync()
                from _ in SetMetadataId(vmInfo, command.NewMetadataId)
                select Unit.Default;
        }

    }
}