using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Events;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines;
using Haipa.Primitives.Resources.Machines.Config;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using LanguageExt;
using Rebus.Bus;
using Rebus.Transport;
using VirtualMachineInfo = Haipa.VmManagement.Data.Full.VirtualMachineInfo;

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

#pragma warning disable 1998
        protected async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureSingleEntry(
            Seq<TypedPsObject<VirtualMachineInfo>> list, Guid id)
#pragma warning restore 1998
        {
            return list.Count > 1
                ? Prelude.Left(new PowershellFailure { Message = $"VM id '{id}' is not unique." })
                : list.HeadOrNone().ToEither(new PowershellFailure { Message = $"VM id '{id}' is not found." });
        }


        protected static Task<Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(Guid id,
            IPowershellEngine engine) =>

            Prelude.Cond<Guid>((c) => c != Guid.Empty)(id).MatchAsync(
                None: () => Seq<TypedPsObject<VirtualMachineInfo>>.Empty,
                Some: (s) => engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm").AddParameter("Id", id)
                    //this a bit dangerous, because there may be other errors causing the 
                    //command to fail. However there seems to be no other way except parsing error response
                    .AddParameter("ErrorAction", "SilentlyContinue")
                ));


        protected Task<Either<PowershellFailure, VirtualMachineMetadata>> EnsureMetadata(VirtualMachineMetadata metadata,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var notes = vmInfo.Value.Notes;

            var metadataIdString = "";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                var metadataIndex = notes.IndexOf("Haipa metadata id: ", StringComparison.InvariantCultureIgnoreCase);
                if (metadataIndex != -1)
                {
                    var metadataEnd = metadataIndex + "Haipa metadata id: ".Length + 36;
                    if (metadataEnd <= notes.Length)
                        metadataIdString = notes.Substring(metadataIndex + "Haipa metadata id: ".Length, 36);

                }
            }


            if (string.IsNullOrWhiteSpace(metadataIdString))
            {
                var newNotes = $"Haipa metadata id: {metadata.Id}";

                return Engine.RunAsync(new PsCommandBuilder().AddCommand("Set-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Notes", newNotes)).MapAsync(u => metadata);

            }

            if (!Guid.TryParse(metadataIdString, out var metadataId))
                throw new InvalidOperationException("Found invalid haipa metadata id in VM notes.");


            if (metadataId != metadata.Id)
                throw new InvalidOperationException("Inconsistent metadata id between VM and expected metadata id.");

            return Prelude.RightAsync<PowershellFailure, VirtualMachineMetadata>(metadata).ToEither();
        }

        protected Task<Either<PowershellFailure, Unit>> SetMetadataId(TypedPsObject<VirtualMachineInfo> vmInfo, Guid metadataId)
        {
            var oldNotes = vmInfo.Value.Notes;
            if (string.IsNullOrWhiteSpace(oldNotes))
                oldNotes = "\n\n\n\n--- DO NOT REMOVE NEXT LINE - REQUIRED FOR HAIPA ---\n";
            
            var startPos = oldNotes.IndexOf("Haipa metadata id: ", StringComparison.Ordinal);

            var notesBeforeMetaData = oldNotes;
            var notesAfterMetaData  = "";

            if (startPos > -1)
            {
                notesBeforeMetaData = oldNotes.Substring(0, startPos);
                var endPos = startPos + "Haipa metadata id: ".Length + Guid.Empty.ToString().Length;

                if (endPos < oldNotes.Length)
                    notesAfterMetaData = oldNotes.Substring(endPos, oldNotes.Length - endPos);

            }

            var newNotes = notesBeforeMetaData +  $"Haipa metadata id: {metadataId}" + notesAfterMetaData;


            return Engine.RunAsync(new PsCommandBuilder().AddCommand("Set-VM").AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Notes", newNotes));


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

        protected static Task<Either<PowershellFailure, VirtualMachineData>> CreateMachineInventory(
            IPowershellEngine engine, HostSettings hostSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            var inventory = new VirtualMachineInventory(engine, hostSettings);
            return inventory.InventorizeVM(vmInfo).ToAsync().ToEither();
        }


    }
}