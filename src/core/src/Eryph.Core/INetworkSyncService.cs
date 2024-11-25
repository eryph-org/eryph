using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface INetworkSyncService
{
    public EitherAsync<Error, Unit> SyncNetworks(CancellationToken cancellationToken);

    public EitherAsync<Error, string[]> ValidateChanges(NetworkProvider[] networkProviders);
}
