using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Runs component enrollment on startup and re-checks periodically to renew before expiry. The
/// initial enrollment is retry-tolerant (see <see cref="ComponentEnrollmentClient"/>), so the
/// component keeps trying if the identity service is not yet available. Registered by the
/// split-runtime hosts that use bus mTLS; eryph-zero (in-memory bus) does not need it.
/// </summary>
internal sealed class ComponentEnrollmentHostedService(
    ComponentEnrollmentClient client,
    ILogger<ComponentEnrollmentHostedService> logger)
    : IHostedService
{
    // How often to re-check whether the certificate has entered its renewal window. Renewal itself
    // only acts when EnsureEnrolledAsync finds the certificate is no longer current.
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
                await client.EnsureEnrolledAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Component enrollment/renewal failed; will retry.");
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
