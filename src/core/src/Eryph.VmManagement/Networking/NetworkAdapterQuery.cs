using System.Threading.Tasks;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Networking
{
    public static class NetworkAdapterQuery
    {
        public static Task<Either<Error, Seq<TypedPsObject<VMNetworkAdapter>>>> GetNetworkAdapters<TVM>(
            TypedPsObject<TVM> vm, IPowershellEngine engine)
        {
            return engine.GetObjectsAsync<VMNetworkAdapter>(
                new PsCommandBuilder().AddCommand("Get-VMNetworkAdapter")
                    .AddParameter("VM", vm.PsObject)).ToError();
        }


        public static Either<Error, TypedPsObject<VMNetworkAdapter>> FindAdapter(Seq<TypedPsObject<VMNetworkAdapter>> sequence,
            string adapterId)
        {
            adapterId = adapterId.Replace("Microsoft:GuestNetwork\\", "Microsoft:");
            return sequence.Find(a => a.Value.Id == adapterId)
                .ToEither(new PowershellFailure {Message = $"could not find network adapter with Id '{adapterId}'"}).ToError();
        }
    }
}