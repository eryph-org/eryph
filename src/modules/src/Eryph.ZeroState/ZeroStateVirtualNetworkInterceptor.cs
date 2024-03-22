﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState
{
    public class ZeroStateVirtualNetworkInterceptor : ZeroStateInterceptorBase<VirtualNetworkChange>
    {
        public ZeroStateVirtualNetworkInterceptor(
            IZeroStateQueue<VirtualNetworkChange> queue)
            : base(queue)
        {
        }

        protected override async Task<Option<VirtualNetworkChange>> DetectChanges(
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is null)
                return None;

            var subnets = await eventData.Context.ChangeTracker.Entries<IpPool>()
                .Map(async e =>
                {
                    var subnetReference = e.Reference(s => s.Subnet);
                    await subnetReference.LoadAsync(cancellationToken);
                    return Optional(subnetReference.TargetEntry);
                })
                .SequenceSerial()
                .Map(e => e.Somes()
                    .Map(s => s.Entity)
                    .OfType<VirtualNetworkSubnet>()
                    .Map(vns => eventData.Context.Entry(vns)));

            var networks = await subnets
                .Concat(eventData.Context.ChangeTracker.Entries<VirtualNetworkSubnet>())
                .Map(async e =>
                {
                    var networkReference = e.Reference(s => s.Network);
                    await networkReference.LoadAsync(cancellationToken);
                    return Optional(networkReference.TargetEntry);
                })
                .SequenceSerial()
                .Map(e => e.Somes());

            var projectIds = networks
                .Concat(eventData.Context.ChangeTracker.Entries<VirtualNetwork>())
                .Map(e => e.Entity.ProjectId)
                .Distinct()
                .ToList();

            return projectIds.Match(
                Empty: () => None,
                More: p => Some(new VirtualNetworkChange
                {
                    ProjectIds = p.ToList(),
                }));
        }
    }
}