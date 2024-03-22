using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent
{
    internal abstract class CatletConfigCommandHandler<TMessage, TResult>: IHandleMessages<OperationTask<TMessage>> 
        where TMessage : class, new()
    {
        public const string DefaultDigits = "0123456789abcdefghijklmnopqrstuvwxyz";
        protected readonly ITaskMessaging _messaging;
        protected readonly ILogger Log;
        protected readonly IPowershellEngine Engine;
        protected OperationTask<TMessage>? Message;

        protected CatletConfigCommandHandler(
            IPowershellEngine engine,
            ITaskMessaging messaging, ILogger log)
        {
            Engine = engine;
            _messaging = messaging;
            Log = log;
        }

        public Task Handle(OperationTask<TMessage> message)
        {
            Message = message;
            return HandleCommand(message.Command)
                .FailOrComplete(_messaging, message);

        }

        protected async Task<Unit> ProgressMessage(string progressMessage)
        {
            if(Message != null)
                await _messaging.ProgressMessage(Message, progressMessage);
            return Unit.Default;
        }

        protected abstract EitherAsync<Error, TResult> HandleCommand(TMessage command);


        public static string LongToString(BigInteger subject, int @base = 36, string digits = DefaultDigits)
        {
            if (@base < 2) throw new ArgumentException("Base must not be less than 2", nameof(@base));
            if (digits.Length < @base) throw new ArgumentException("Not enough Digits for the base", nameof(digits));


            var result = new StringBuilder();
            var sign = 1;

            if (subject < 0) subject *= sign = -1;

            do
            {
                result.Insert(0, digits[(int) (subject % @base)]);
                subject /= @base;
            } while (subject > 0);

            if (sign == -1) result.Insert(0, '-');

            return result.ToString();
        }

        protected EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> EnsureSingleEntry(
            Seq<TypedPsObject<VirtualMachineInfo>> list, Guid id)
        {
            return (list.Count > 1
                ? Prelude.Left(Error.New($"VM id '{id}' is not unique."))
                : list.HeadOrNone().ToEither(Error.New($"VM id '{id}' is not found."))).ToAsync();
        }


        protected static EitherAsync<Error, Seq<TypedPsObject<VirtualMachineInfo>>> GetVmInfo(Guid id,
            IPowershellEngine engine)
        {
            return Prelude.Cond<Guid>(c => c != Guid.Empty)(id).MatchAsync(
                None: () => Seq<TypedPsObject<VirtualMachineInfo>>.Empty,
                Some: s => engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm").AddParameter("Id", id)
                    //this a bit dangerous, because there may be other errors causing the 
                    //command to fail. However there seems to be no other way except parsing error response
                    .AddParameter("ErrorAction", "SilentlyContinue")
                )).ToError().ToAsync();
        }


        protected Task<Either<Error, CatletMetadata>> EnsureMetadata(
            CatletMetadata metadata,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var notes = vmInfo.Value.Notes;

            var metadataIdString = "";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                var metadataIndex = notes.IndexOf("eryph metadata id: ", StringComparison.InvariantCultureIgnoreCase);
                if (metadataIndex != -1)
                {
                    var metadataEnd = metadataIndex + "eryph metadata id: ".Length + 36;
                    if (metadataEnd <= notes.Length)
                        metadataIdString = notes.Substring(metadataIndex + "eryph metadata id: ".Length, 36);
                }
            }


            if (string.IsNullOrWhiteSpace(metadataIdString))
            {
                var newNotes = $"eryph metadata id: {metadata.Id}";

                return Engine.RunAsync(new PsCommandBuilder().AddCommand("Set-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Notes", newNotes)).MapAsync(u => metadata).ToError();
            }

            if (!Guid.TryParse(metadataIdString, out var metadataId))
                throw new InvalidOperationException("Found invalid eryph metadata id in VM notes.");


            if (metadataId != metadata.Id)
                throw new InvalidOperationException("Inconsistent metadata id between VM and expected metadata id.");

            return Prelude.RightAsync<Error, CatletMetadata>(metadata).ToEither();
        }

        protected EitherAsync<Error, Unit> SetMetadataId(TypedPsObject<VirtualMachineInfo> vmInfo,
            Guid metadataId)
        {
            var oldNotes = vmInfo.Value.Notes;
            if (string.IsNullOrWhiteSpace(oldNotes))
                oldNotes = "\n\n\n\n--- DO NOT REMOVE NEXT LINE - REQUIRED FOR ERYPH ---\n";

            var startPos = oldNotes.IndexOf("eryph metadata id: ", StringComparison.Ordinal);

            var notesBeforeMetaData = oldNotes;
            var notesAfterMetaData = "";

            if (startPos > -1)
            {
                notesBeforeMetaData = oldNotes.Substring(0, startPos);
                var endPos = startPos + "eryph metadata id: ".Length + Guid.Empty.ToString().Length;

                if (endPos < oldNotes.Length)
                    notesAfterMetaData = oldNotes.Substring(endPos, oldNotes.Length - endPos);
            }

            var newNotes = notesBeforeMetaData + $"eryph metadata id: {metadataId}" + notesAfterMetaData;


            return Engine.RunAsync(new PsCommandBuilder().AddCommand("Set-VM").AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Notes", newNotes)).ToAsync().ToError();
        }


        protected static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> EnsureNameConsistent(
            TypedPsObject<VirtualMachineInfo> vmInfo, CatletConfig config, IPowershellEngine engine)
        {
            return Prelude.Cond<(string currentName, string newName)>(names =>
                    !string.IsNullOrWhiteSpace(names.newName) &&
                    !names.newName.Equals(names.currentName, StringComparison.InvariantCulture))((vmInfo.Value.Name,
                    config.Name))
                .Match(
                    None: () => vmInfo,
                    Some: some => VirtualMachine.Rename(engine, vmInfo, config.Name));
        }

        protected static EitherAsync<Error, VirtualMachineData> CreateMachineInventory(
            IPowershellEngine engine, VmHostAgentConfiguration vmHostAgentConfig,
            TypedPsObject<VirtualMachineInfo> vmInfo, IHostInfoProvider hostInfoProvider)
        {
            var inventory = new VirtualMachineInventory(engine, vmHostAgentConfig, hostInfoProvider);
            return inventory.InventorizeVM(vmInfo).ToAsync();
        }


    }
}