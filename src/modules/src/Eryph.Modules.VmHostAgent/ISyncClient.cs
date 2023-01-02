using System.Threading;
using Eryph.Core.Network;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent;

public interface ISyncClient
{
    Aff<Unit> SendSyncCommand(string command, CancellationToken cancellationToken);
    Aff<bool> CheckRunning(CancellationToken cancellationToken);
    Aff<string[]> ValidateChanges(NetworkProvider[] networkProviders, CancellationToken cancellationToken);
}