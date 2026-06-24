using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;
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
    /// <summary>
    /// Ensures the component holds a current certificate, renewing or enrolling as needed. When
    /// <paramref name="force"/> is set, a renewal is performed even if the stored certificate is not yet
    /// in its renewal window — an operator-triggered refresh (e.g. to pick up a rotated CA, or to verify
    /// renewal works) rather than the scheduled "renew when due" check.
    /// </summary>
    public async Task EnsureEnrolledAsync(CancellationToken cancellationToken, bool force = false)
    {
        if (!force && store.HasCurrentCertificate())
            return;

        var haveValid = store.HasValidCertificate();

        // A forced renewal needs a valid certificate to authenticate the renew endpoint. With none
        // (missing/expired), renewal cannot succeed and the one-time enrollment token was consumed at
        // first enrollment, so the token path cannot recover it either — surface that clearly instead of
        // a silent, doomed token attempt. Recovery is re-enrollment (a fresh enrollment file).
        if (force && !haveValid)
        {
            logger.LogError(
                "Cannot force a renewal for {ComponentType}: no valid certificate to authenticate with. "
                + "The component must be re-enrolled (a new enrollment token) to recover.",
                identity.ComponentType);
            return;
        }

        // If a still-valid certificate exists, the component renews by authenticating with that
        // certificate (mTLS) — the one-time token cannot be reused. A forced refresh of a still-current
        // certificate takes this path too. Otherwise it has no usable certificate and must enroll with
        // the token (blocking, so startup waits for the identity service).
        await EnrollWithRetryAsync(!haveValid, haveValid, cancellationToken);
    }

    private async Task EnrollWithRetryAsync(bool blocking, bool renew, CancellationToken cancellationToken)
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

                // Renewal authenticates with the current certificate (presented by the transport's TLS
                // layer) against the renew endpoint; first enrollment presents the one-time token. Both
                // request a brand-new key pair so renewal also rotates the private key.
                var result = renew
                    ? await transport.RenewAsync(request, cancellationToken)
                    : await transport.EnrollAsync(request, cancellationToken);
                store.Save(clientKey.ExportPkcs8PrivateKey(), serverKey.ExportPkcs8PrivateKey(), result);

                logger.LogInformation(
                    "Component {ComponentType} {Action} on attempt {Attempt} (component {ComponentId}).",
                    identity.ComponentType, renew ? "renewed" : "enrolled", attempt, result.ComponentId);
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
                    // A still-valid certificate is already in place (this is a renewal), so never throw —
                    // that would crash a healthy component on a transient blip. But distinguish the cause:
                    // a non-transient failure (used/expired enrollment token, TLS misconfiguration) cannot
                    // succeed on the next check either, so surface it at Error with an actionable message
                    // instead of a misleading "will retry"; the component keeps running on its current
                    // certificate until it expires.
                    if (IsNonTransient(ex))
                        logger.LogError(
                            ex,
                            "Component certificate renewal cannot succeed (non-retryable error); the component "
                            + "will keep running on its current certificate until it expires. A new enrollment "
                            + "token/file is required to renew.");
                    else
                        logger.LogWarning(
                            ex, "Component certificate renewal attempt failed; will retry on the next check.");
                    return;
                }

                if (IsNonTransient(ex))
                {
                    // A non-retryable failure (4xx such as an expired/used token or wrong host/type, a
                    // malformed response, or a TLS trust failure): retrying can never succeed and would
                    // wedge startup forever. Surface it so the operator gets an actionable failure. If the
                    // token was already consumed server-side, a new enrollment file is required.
                    logger.LogError(
                        ex,
                        "Component enrollment failed with a non-retryable error; aborting. If the enrollment "
                        + "token was already consumed, a new enrollment file must be issued.");
                    throw;
                }

                // Transient (5xx, timeout/429, or a connection failure): the identity service may still
                // be starting or be briefly unavailable. Never fail on this — back off and keep trying.
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

    // An error that retrying can never fix, so blocking startup must not loop on it forever:
    //  - a malformed/empty success body (JsonException): a server/version mismatch, not an outage;
    //  - a client-error (4xx) response, except 408/429 which are transient by definition; and
    //  - a TLS trust failure (wrong pinned CA, host-name mismatch): it surfaces as an HttpRequestException
    //    with no StatusCode wrapping an AuthenticationException, and is a misconfiguration retrying cannot
    //    fix. A plain connection failure has no such inner exception and stays transient (the identity
    //    service may simply not be listening yet).
    private static bool IsNonTransient(Exception ex)
    {
        switch (ex)
        {
            case JsonException:
                return true;
            case HttpRequestException { StatusCode: { } status }:
                return (int)status is >= 400 and < 500
                       && status is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests);
        }

        for (var inner = ex; inner is not null; inner = inner.InnerException)
            if (inner is AuthenticationException)
                return true;
        return false;
    }
}
