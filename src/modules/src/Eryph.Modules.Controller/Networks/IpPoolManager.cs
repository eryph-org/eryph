using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

internal class IpPoolManager : IIpPoolManager
{
    private readonly IStateStore _stateStore;

    public IpPoolManager(IStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public EitherAsync<Error,IpPoolAssignment> AcquireIp(Guid subnetId, string poolName, CancellationToken cancellationToken)
    {
        return Prelude.TryAsync(async () =>
        {
            var pool = await _stateStore.For<IpPool>()
                .GetBySpecAsync(new IpPoolSpecs.GetByName(subnetId, poolName), cancellationToken)
                .ConfigureAwait(false);

            if (pool == null)
                throw new NotFoundException($"IpPool {poolName} not found for subnet id {subnetId}");

            var lastNumber = pool.Counter;
            GetPoolAddresses(pool, out var ipNetwork, out var firstIp, out var lastIp);

            var startNumber = IPNetwork2.ToBigInteger(firstIp);
            var endNumber = IPNetwork2.ToBigInteger(lastIp);

            var space = endNumber - startNumber;

            var totalAssigned =
                await _stateStore.Read<IpPoolAssignment>()
                    .CountAsync(new IpPoolSpecs.GetAssignments(pool.Id), cancellationToken);

            if (totalAssigned >= space)
                throw new InvalidOperationException($"IpPool {pool.Id} has no more free ips.");


            async Task FindFreeNumber()
            {
                // ReSharper disable once AccessToModifiedClosure
                while (startNumber + lastNumber < endNumber)
                {

                    var minNumber = (await
                        _stateStore.Read<IpPoolAssignment>()
                            // ReSharper disable once AccessToModifiedClosure
                            .GetBySpecAsync(new IpPoolSpecs.GetMinNumberStartingAt(pool.Id, lastNumber),
                                cancellationToken))?.Number;

                    if (!minNumber.HasValue)
                    {
                        break;
                    }

                    lastNumber = minNumber.Value;
                    lastNumber++;

                }
            }

            await FindFreeNumber().ConfigureAwait(false);


            if (startNumber + lastNumber > endNumber)
            {
                //rollover (there has to be a gap as there are free numbers)
                lastNumber = 0;
                await FindFreeNumber().ConfigureAwait(false);
            }

            var addressNo = startNumber + lastNumber;
            var foundIp = IPNetwork2.ToIPAddress(addressNo, ipNetwork.AddressFamily);
            var assignment = new IpPoolAssignment
            {
                Id = Guid.NewGuid(),
                Number = lastNumber,
                IpAddress = foundIp.ToString(),
                PoolId = pool.Id,
                SubnetId = subnetId
            };

            await _stateStore.For<IpPoolAssignment>().AddAsync(assignment, cancellationToken).ConfigureAwait(false);
            pool.Counter = lastNumber;

            return assignment;

        }).ToEither();
    }

    private static void GetPoolAddresses(IpPool pool, out IPNetwork2 ipNetwork, out IPAddress firstIp,
        out IPAddress? lastIp)
    {
        ipNetwork = IPNetwork2.Parse(pool.IpNetwork);
        firstIp = IPAddress.Parse(pool.FirstIp);
        lastIp = IPAddress.Parse(pool.LastIp);

        if (ipNetwork == null)
            throw new InvalidDataException($"IpPool {pool.Id} has invalid network '{pool.IpNetwork}'");

        if (firstIp == null)
            throw new InvalidDataException($"IpPool {pool.Id} has invalid first ip '{pool.FirstIp}'");

        if (lastIp == null)
            throw new InvalidDataException($"IpPool {pool.Id} has invalid first ip '{pool.LastIp}'");
    }

}