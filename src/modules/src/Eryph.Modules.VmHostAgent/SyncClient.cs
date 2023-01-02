using System;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Network;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent;

public class SyncClient : ISyncClient
{
    public Aff<Unit> SendSyncCommand(string command, CancellationToken cancellationToken)
    {
        return SendSyncCommandInternal<object>(command, null, cancellationToken)
            .ToAsync().Map(_ => Unit.Default).ToAff(l => l);

    }

    public Aff<bool> CheckRunning(CancellationToken cancellationToken)
    {
        return SendSyncCommandInternal<bool>("STATUS", null, cancellationToken)
            .ToAsync()
            .Bind(o => o.ToEitherAsync(Error.New("Status response expected")))
            .ToAff(l => l);

    }

    public Aff<string[]> ValidateChanges(NetworkProvider[] networkProviders, CancellationToken cancellationToken)
    {
        return SendSyncCommandInternal<string[]>("VALIDATE_CHANGES", networkProviders, cancellationToken)
            .ToAsync()
            .Bind(o => o.ToEitherAsync(Error.New("Change response expected")))
            .ToAff(l => l);

    }


    private async Task<Either<Error, Option<TResponse>>> SendSyncCommandInternal<TResponse>(string command, [CanBeNull] object data, CancellationToken cancellationToken)
    {
        return await Prelude.TryAsync(async () =>
        {
            var pipeClient =
                new NamedPipeClientStream(".", "eryph_hostagent_sync",
                    PipeDirection.InOut, PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Impersonation);

            await pipeClient.ConnectAsync(cancellationToken);

            var ss = new StreamString(pipeClient);

            var commandObject = new SyncServiceCommand
            {
                CommandName = command
            };

            if (data != null) commandObject.Data = JsonSerializer.SerializeToElement(data);

            await ss.WriteCommand(commandObject, cancellationToken);

            var response = await ss.ReadString(cancellationToken);

            var responseObject = new SyncServiceResponse { Response = "FAILED" };
            if (response != null) responseObject = JsonSerializer.Deserialize<SyncServiceResponse>(response);

            return responseObject?.Response switch
            {
                "INVALID" => throw new InvalidOperationException($"Agent command {command} is invalid"),
                "FAILED" => throw new InvalidOperationException($"Agent command {command} failed"),
                "PERMISSION_DENIED" => throw new UnauthorizedAccessException(),
                _ => !(responseObject?.Data).HasValue
                    ? Option<TResponse>.None
                    : Option<TResponse>.Some((responseObject!.Data.Value).Deserialize<TResponse>())
            };
        }).ToEither( ex => Error.New(ex.Message));


    }

}