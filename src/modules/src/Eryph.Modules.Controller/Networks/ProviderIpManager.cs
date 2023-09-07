using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Network;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

internal class ProviderIpManager : BaseIpManager, IProviderIpManager
{

    public ProviderIpManager(IStateStore stateStore, IIpPoolManager poolManager) : base(stateStore, poolManager)
    {
    }

    public EitherAsync<Error, IPAddress[]> ConfigureFloatingPortIps(NetworkProvider provider, FloatingNetworkPort port,
        CancellationToken cancellationToken)
    {

        var portProvider = new []
        {
            new PortProvider(AddressFamily.InterNetwork, provider.Name, Option<string>.None, Option<string>.None)
        };


        var getPortAssignments =
            Prelude.TryAsync(_stateStore.For<IpAssignment>().ListAsync(new IPAssignmentSpecs.GetByPort(port.Id),
                    cancellationToken))
                .ToEither(f => Error.New(f));

        return from portAssignments in getPortAssignments
            from validAssignments in portAssignments.Map(
                    assignment => CheckAssignmentConfigured(assignment, port).ToAsync())
                .TraverseSerial(l => l.AsEnumerable())
                .Map(e => e.Flatten())

            from validAndNewAssignments in portProvider.Map(portNetwork =>
            {
                var providerNameString = portNetwork.ProviderName.IfNone("default");
                var subnetNameString = portNetwork.SubnetName.IfNone("default");
                var poolNameString = portNetwork.PoolName.IfNone("default");

                return

                    from subnet in _stateStore.Read<ProviderSubnet>().IO
                        .GetBySpecAsync(new SubnetSpecs.GetByProviderName(providerNameString, subnetNameString), cancellationToken)
                        .Bind(r => r.ToEitherAsync(
                            Error.New($"Subnet {subnetNameString} not found for provider {providerNameString}.")))

                    let existingAssignment = CheckAssignmentExist(validAssignments, subnet, poolNameString)

                    from assignment in existingAssignment.IsSome ?
                        existingAssignment.ToEitherAsync(Errors.None)
                        : from newAssignment in _poolManager.AcquireIp(subnet.Id, poolNameString, cancellationToken)
                            .Map(a => (IpAssignment)a)
                        let _ = UpdatePortAssignment(port, newAssignment)
                        select newAssignment
                    select assignment;

            }).TraverseParallel(l => l)

            select validAndNewAssignments
                .Select(x => IPAddress.Parse((string)x.IpAddress)).ToArray();

    }


    private async Task<Either<Error, Option<IpAssignment>>> CheckAssignmentConfigured(IpAssignment assignment, FloatingNetworkPort port)
    {
        var subnetName = "";
        var poolName = "";

        await _stateStore.LoadPropertyAsync(assignment, x => x.Subnet);
        if (assignment.Subnet is ProviderSubnet providerSubnet)
        {
            subnetName = providerSubnet.Name;

        }

        if (assignment is IpPoolAssignment poolAssignment)
        {
            await _stateStore.LoadPropertyAsync(poolAssignment, x => x.Pool);
            poolName = poolAssignment.Pool.Name;
        }

        if (port.SubnetName == subnetName && (string.IsNullOrWhiteSpace(poolName) && port.PoolName == poolName))
            return Prelude.Right<Error, Option<IpAssignment>>(assignment);

        // remove invalid
        await _stateStore.For<IpAssignment>().DeleteAsync(assignment);
        return Prelude.Right<Error, Option<IpAssignment>>(Option<IpAssignment>.None);

    }

    private record PortProvider(
        AddressFamily AddressFamily,
        Option<string> ProviderName,
        Option<string> SubnetName,
        Option<string> PoolName);
}