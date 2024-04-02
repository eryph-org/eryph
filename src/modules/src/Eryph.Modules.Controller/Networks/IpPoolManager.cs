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
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

internal class IpPoolManager : IIpPoolManager
{
    private readonly ILogger _logger;
    private readonly IStateStore _stateStore;

    public IpPoolManager(
        ILogger logger,
        IStateStore stateStore)
    {
        _logger = logger;
        _stateStore = stateStore;
    }

    public EitherAsync<Error, IpPoolAssignment> AcquireIp(
        Guid subnetId,
        string poolName,
        CancellationToken cancellationToken = default) => TryAsync(async () =>
        {
            var pool = await _stateStore.For<IpPool>().GetBySpecAsync(
                new IpPoolSpecs.GetByName(subnetId, poolName),
                cancellationToken);

            if (pool is null)
                throw new InvalidOperationException($"IP pool {poolName} not found for subnet id {subnetId}");

            if(!IPNetwork.TryParse(pool.IpNetwork, out var ipNetwork))
                throw new InvalidOperationException($"IP pool {pool.Id} has invalid network '{pool.IpNetwork}'");
            if(!IPAddress.TryParse(pool.FirstIp, out var firstIp))
                throw new InvalidOperationException($"IP pool {pool.Id} has invalid first IP '{pool.FirstIp}");
            if (!IPAddress.TryParse(pool.NextIp, out var nextIp))
                throw new InvalidOperationException($"IP pool {pool.Id} has invalid next IP '{pool.NextIp}'");
            if (!IPAddress.TryParse(pool.LastIp, out var lastIp))
                throw new InvalidOperationException($"IP pool {pool.Id} has invalid last IP '{pool.NextIp}'");

            var firstIpBigInt = IPNetwork.ToBigInteger(firstIp);

            var poolSize = (int)(IPNetwork.ToBigInteger(lastIp) - firstIpBigInt) + 1;
            var nextNumber = (int)(IPNetwork.ToBigInteger(nextIp) - firstIpBigInt);

            var totalAssigned = await _stateStore.Read<IpPoolAssignment>()
                .CountAsync(new IpPoolSpecs.GetAssignments(pool.Id), cancellationToken);

            if (totalAssigned >= poolSize)
                throw new InvalidOperationException($"IP pool {poolName}({pool.Id}) has no more free IP addresses.");

            var isNextNumberFree = await IsNumberFree(pool.Id, nextNumber, cancellationToken);
            if (!isNextNumberFree)
            {
                _logger.LogInformation("Next IP {NextIp} of IP pool {PoolName}({PoolId}) already in use. Looking for next free IP.",
                    pool.NextIp, pool.Name, pool.Id);
                nextNumber = await FindNextFreeNumber(pool.Id, nextNumber + 1, poolSize, cancellationToken)
                    .IfNoneAsync((Func<int>)(() => throw new InvalidOperationException(
                        $"IP pool {poolName}({pool.Id}) has no more free IP addresses.")));
            }

            var assignment = new IpPoolAssignment
            {
                Id = Guid.NewGuid(),
                Number = nextNumber,
                IpAddress = IPNetwork.ToIPAddress(firstIpBigInt + nextNumber, ipNetwork.AddressFamily)
                    .ToString(),
                PoolId = pool.Id,
                SubnetId = subnetId
            };
            await _stateStore.For<IpPoolAssignment>().AddAsync(assignment, cancellationToken);

            pool.NextIp = totalAssigned < poolSize - 1
                ? await FindNextFreeNumber(pool.Id, nextNumber + 1, poolSize, cancellationToken)
                    .Match(
                        Some: n => IPNetwork.ToIPAddress(
                                firstIpBigInt + n, ipNetwork.AddressFamily)
                            .ToString(),
                    None: () =>  pool.FirstIp)
                : pool.FirstIp;

            await _stateStore.For<IpPool>().UpdateAsync(pool, cancellationToken);

            return assignment;
        }).ToEither();

    private async Task<Option<int>> FindNextFreeNumber(
        Guid poolId,
        int startAt,
        int poolSize,
        CancellationToken cancellationToken)
    {
        var candidate = startAt;
        while (candidate < poolSize)
        {
            if (await IsNumberFree(poolId, candidate, cancellationToken))
                return Some(candidate);
            
            candidate++;
        }

        candidate = 0;
        while (candidate < startAt)
        {
            if (await IsNumberFree(poolId, candidate, cancellationToken))
                return Some(candidate);

            candidate++;
        }

        return None;
    }

    private async Task<bool> IsNumberFree(
        Guid poolId,
        int number,
        CancellationToken cancellationToken)
    {
        return !await _stateStore.For<IpPoolAssignment>().AnyAsync(
            new IpPoolAssignmentSpecs.GetByNumber(poolId, number),
            cancellationToken);
    }
}
