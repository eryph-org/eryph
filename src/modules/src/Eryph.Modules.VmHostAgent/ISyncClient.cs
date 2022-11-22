using System.Threading;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent;

public interface ISyncClient
{
    Aff<Unit> SendSyncCommand(string command, CancellationToken cancellationToken);
    Aff<bool> CheckRunning(CancellationToken cancellationToken);
}