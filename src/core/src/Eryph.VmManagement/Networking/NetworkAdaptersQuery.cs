using System.Threading.Tasks;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace Eryph.VmManagement.Networking
{
    public static class NetworkAdapterQuery
    {
        public static Task<Either<PowershellFailure, Seq<TypedPsObject<VMNetworkAdapter>>>> GetNetworkAdapters<TVM>(
            TypedPsObject<TVM> vm, IPowershellEngine engine)
        {
            return engine.GetObjectsAsync<VMNetworkAdapter>(
                new PsCommandBuilder().AddCommand("Get-VMNetworkAdapter")
                    .AddParameter("VM", vm.PsObject));
        }


        public static Either<PowershellFailure, TypedPsObject<VMNetworkAdapter>> FindAdapter(Seq<TypedPsObject<VMNetworkAdapter>> sequence,
            string adapterId)
        {
            adapterId = adapterId.Replace("Microsoft:GuestNetwork\\", "Microsoft:");
            return sequence.Find(a => a.Value.Id == adapterId)
                .ToEither(new PowershellFailure {Message = $"could not find network adapter with Id '{adapterId}'"});
        }
    }
}