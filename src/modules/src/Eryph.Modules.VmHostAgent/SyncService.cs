using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent;

internal class SyncService : BackgroundService
{ 
    readonly ILogger _logger;
    private readonly IAgentControlService _controlService;

    public SyncService(ILogger<SyncService> logger, IAgentControlService controlService)
    {
        _logger = logger;
        _controlService = controlService;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var pipeServer =
                NamedPipeServerStreamAcl.Create("eryph_hostagent_sync",

                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous,
                    0, 0, CreateSystemIOPipeSecurity());

            var isIdle = true;
            try
            {
                await pipeServer.WaitForConnectionAsync(stoppingToken);
                isIdle = false;
                var ss = new StreamString(pipeServer);


                var command = await ss.ReadString(stoppingToken);

                var hasPermission = false;
                var commandValid = true;
                switch (command)
                {
                    case "STATUS":
                    {
                        hasPermission = true;
                        break;
                    }
                    case "RECREATE_PORTS": break;
                    case "REBUILD_NETWORKS": break;
                    case "STOP_OVN": break;
                    case "START_OVN": break;
                    default:
                        commandValid = false;
                        break;
                }

                if (commandValid && !hasPermission)
                {
                    pipeServer.RunAsClient(() =>
                    {
                        AdminGuard.InElevatedProcess(() => Unit.Default,
                            () =>
                            {
                                hasPermission = true;
                                return Unit.Default;
                            });
                    });
                }

                if (!commandValid)
                    await ss.WriteString("INVALID", stoppingToken);
                else
                {
                    if (hasPermission)
                    {
                        var response = await RunCommand(command);
                        await ss.WriteString(response, stoppingToken);
                    }
                    else
                        await ss.WriteString("PERMISSION_DENIED", stoppingToken);
                }

                pipeServer.WaitForPipeDrain();
            }
            catch (Exception ex)
            {
                if (!isIdle)
                    _logger.LogDebug(ex, "Exception in sync service");
            }
            finally
            {
                try
                {
                    pipeServer.Disconnect();
                }
                catch (Exception)
                {

                }
            }
        }
    }

    private async Task<string> RunCommand(string command)
    {
        AgentService service;
        AgentServiceOperation operation;
        switch (command)
        {
            case "STATUS": return "RUNNING";
            case "RECREATE_PORTS": return "DONE";
            case "REBUILD_NETWORKS": return "DONE";
            case "STOP_OVN":
                service = AgentService.OVNController;
                operation = AgentServiceOperation.Stop;
                break;
            case "START_OVN":
                service = AgentService.OVNController;
                operation = AgentServiceOperation.Start;
                break;
            default: return "INVALID";
        }

        var succeeded = await _controlService.SendControlEvent(
            service, operation, CancellationToken.None);

        return succeeded ? "DONE" : "FAILED";

    }

    private static PipeSecurity CreateSystemIOPipeSecurity()
    {
        var pipeSecurity = new PipeSecurity();

        var id = new SecurityIdentifier(
            WellKnownSidType.AuthenticatedUserSid, null);

        // Allow Everyone read and write access to the pipe. 
        pipeSecurity.SetAccessRule(
            new PipeAccessRule(id, PipeAccessRights.ReadWrite, 
                AccessControlType.Allow));

        return pipeSecurity;
    }


}