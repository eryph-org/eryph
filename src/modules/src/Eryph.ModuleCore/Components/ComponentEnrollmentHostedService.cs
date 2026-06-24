using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Re-checks the component certificate periodically and renews it (by authenticating with the current
/// certificate against the identity renew endpoint) once it enters its renewal window, so a long-lived
/// component renews without a restart. Only the split-runtime hosts that use bus mTLS register it
/// (DI-controlled, via <see cref="ComponentMtlsTransport.AddRenewal"/>); eryph-zero (in-memory bus)
/// does not. The renewed certificate is written to the store atomically and is picked up the next time
/// a TLS consumer reads the file: the bus on its next reconnect, listeners on restart. Renewal is well
/// inside the certificate's validity (the renewal window leads expiry), so a reconnect/restart at any
/// later point loads the renewed certificate — no in-flight re-handshake is required.
/// </summary>
internal sealed class ComponentEnrollmentHostedService(
    ComponentRenewalContext context,
    ILogger<ComponentEnrollmentHostedService> logger)
    : IHostedService
{
    // How often to re-check whether the certificate has entered its renewal window. Renewal itself
    // only acts when the certificate is no longer current (EnsureEnrolledAsync is a no-op otherwise).
    private static readonly TimeSpan RenewalCheckInterval = TimeSpan.FromHours(1);

    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop = RunAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Serialize with an operator-forced renewal so the two cannot POST /renew concurrently.
                await context.RenewalLock.WaitAsync(cancellationToken);
                try
                {
                    await ComponentEnrollment.EnsureEnrolledAsync(
                        context.Store, context.Identity, context.EndpointResolver, context.Options,
                        context.TrustAnchorBundlePath, context.LoggerFactory, cancellationToken);
                }
                finally
                {
                    context.RenewalLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Component certificate renewal check failed; will retry.");
            }

            try
            {
                await Task.Delay(RenewalCheckInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop; }
            catch (OperationCanceledException) { }
        }

        // The loop has finished; dispose the CTS so its wait handle is not leaked.
        _stopping.Dispose();
    }
}
