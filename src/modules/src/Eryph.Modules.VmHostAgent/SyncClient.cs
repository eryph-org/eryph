using System;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent;

internal class SyncClient : ISyncClient
{
    public Aff<Unit> SendSyncCommand(string command, CancellationToken cancellationToken)
    {
        return SendSyncCommandInternal(command, cancellationToken)
            .ToAsync().Map(_ => Unit.Default).ToAff(l => l);

    }

    public Aff<bool> CheckRunning(CancellationToken cancellationToken)
    {
        return SendSyncCommandInternal("STATUS", cancellationToken)
            .ToAsync().Map(s => s == "RUNNING").ToAff(l => l);

    }

    public async Task<Either<Error, string>> SendSyncCommandInternal(string command, CancellationToken cancellationToken)
    {
        return await Prelude.TryAsync(async () =>
        {
            var pipeClient =
                new NamedPipeClientStream(".", "eryph_hostagent_sync",
                    PipeDirection.InOut, PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Impersonation);

            await pipeClient.ConnectAsync(cancellationToken);

            var ss = new StreamString(pipeClient);
            await ss.WriteString(command, cancellationToken);

            var response = await ss.ReadString(cancellationToken);

            return response switch
            {
                "INVALID" => throw new InvalidOperationException($"Agent command {command} is invalid"),
                "FAILED" => throw new InvalidOperationException($"Agent command {command} failed"),
                "PERMISSION_DENIED" => throw new UnauthorizedAccessException(),
                _ => response
            };
        }).ToEither( ex => Error.New(ex.Message));


    }

}