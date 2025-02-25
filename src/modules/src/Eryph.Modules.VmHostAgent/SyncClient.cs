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

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

public class SyncClient : ISyncClient
{
    public Aff<Unit> SendSyncCommand(
        string command,
        CancellationToken cancellationToken) =>
        from _ in SendSyncCommandInternal<object>(command, null, cancellationToken)
        select unit;

    public Aff<bool> CheckRunning(CancellationToken cancellationToken) =>
        from response in SendSyncCommandInternal<bool>("STATUS", null, cancellationToken)
        from result in response.ToAff("Status result expected")
        select result;

    public Aff<string[]> ValidateChanges(
        NetworkProvider[] networkProviders,
        CancellationToken cancellationToken) =>
        from response in SendSyncCommandInternal<string[]>("VALIDATE_CHANGES", networkProviders, cancellationToken)
        from result in response.ToAff(Error.New("Validation result expected"))
        select result;

    private Aff<Option<TResponse>> SendSyncCommandInternal<TResponse>(
        string command,
        object? data,
        CancellationToken cancellationToken) =>
        from responseJson in AffMaybe<string>(async () =>
        {
            await using var pipeClient = new NamedPipeClientStream(".", "eryph_hostagent_sync",
                PipeDirection.InOut, PipeOptions.Asynchronous,
                TokenImpersonationLevel.Impersonation);

            await pipeClient.ConnectAsync(cancellationToken);

            var stream = new StreamString(pipeClient);

            var commandObject = new SyncServiceCommand
            {
                CommandName = command,
                Data = data is not null
                    ? JsonSerializer.SerializeToElement(data)
                    : null
            };

            await stream.WriteCommand(commandObject, cancellationToken);

            return await stream.ReadString(cancellationToken);
        })
        from _1 in guard(notEmpty(responseJson),
            Error.New("The response of the sync service is empty."))
        from response in Eff(() => JsonSerializer.Deserialize<SyncServiceResponse>(responseJson))
            .MapFail(e => Error.New("The response of the sync service is invalid.", e))
        from _2 in guardnot(response.Response == "INVALID",
            Error.New($"The host agent command '{command}' is invalid."))
        from _3 in guardnot(response.Response == "FAILED",
            Error.New($"The host agent command '{command}' failed. Error: '{(notEmpty(response.Error) ? response.Error : "unknown")}'."))
        from _4 in guardnot(response.Response == "PERMISSION_DENIED",
            Error.New($"No permission to access the sync service. Make sure you are elevated."))
        from responseData in Optional(response.Data)
            .Map(d => Eff(() => d.Deserialize<TResponse>())
                .MapFail(e => Error.New("The data of the sync service response is invalid.", e)))
            .Sequence()
        select responseData;
}
