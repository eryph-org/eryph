using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Ensures the component holds a current mTLS certificate: if none is stored (or it has entered its
/// renewal window) it generates a key pair, enrolls with the identity service and persists the
/// result. Enrollment is retried with exponential backoff so a component that starts before — or at
/// the same time as — the identity service does not fail; it keeps trying until enrollment succeeds
/// or it is cancelled.
/// </summary>
public sealed class ComponentEnrollmentClient(
    IEnrollmentTransport transport,
    IComponentCertificateStore store,
    ComponentIdentity identity,
    ComponentEnrollmentClientOptions options,
    ILogger<ComponentEnrollmentClient> logger)
{
    public async Task EnsureEnrolledAsync(CancellationToken cancellationToken)
    {
        if (store.HasCurrentCertificate())
            return;

        // If a still-valid (but renewal-due) certificate exists, the component can keep running on
        // it; otherwise it must enroll before it can connect to the bus.
        var haveValid = store.HasValidCertificate();
        await EnrollWithRetryAsync(blocking: !haveValid, cancellationToken);
    }

    private async Task EnrollWithRetryAsync(bool blocking, CancellationToken cancellationToken)
    {
        var delay = options.InitialRetryDelay;
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            try
            {
                using var clientKey = RSA.Create(2048);
                using var serverKey = RSA.Create(2048);
                var serverDnsNames = options.ServerDnsNames.Count > 0
                    ? options.ServerDnsNames
                    : new[] { identity.MachineName };
                var request = new ComponentEnrollmentRequest
                {
                    ComponentType = identity.ComponentType,
                    Fqdn = identity.MachineName,
                    PublicKey = clientKey.ExportSubjectPublicKeyInfo(),
                    ServerPublicKey = serverKey.ExportSubjectPublicKeyInfo(),
                    ServerDnsNames = serverDnsNames,
                    Token = options.Token,
                };

                var result = await transport.EnrollAsync(request, cancellationToken);
                store.Save(clientKey.ExportPkcs8PrivateKey(), serverKey.ExportPkcs8PrivateKey(), result);

                logger.LogInformation(
                    "Component {ComponentType} enrolled on attempt {Attempt} (component {ComponentId}).",
                    identity.ComponentType, attempt, result.ComponentId);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (!blocking)
                {
                    // A still-valid certificate is already in place (this is a renewal). Do not loop
                    // here — the hosted service retries on its next interval. Only the initial
                    // enrollment (no usable certificate yet) blocks and retries until it succeeds.
                    logger.LogWarning(
                        ex, "Component certificate renewal attempt failed; will retry on the next check.");
                    return;
                }

                // No usable certificate: the identity service may still be starting or be briefly
                // unavailable. Never fail the component on the first attempt — back off and keep trying.
                logger.LogWarning(
                    ex,
                    "Component enrollment attempt {Attempt} failed; retrying in {Delay}. "
                    + "The identity service may still be starting.",
                    attempt, delay);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, options.MaxRetryDelay.Ticks));
            }
        }
    }
}
