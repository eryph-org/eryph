using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Renews the component's certificate on demand when an operator requests it, in the running process
/// (so the renewed material is written and the broker user re-ensured), rather than waiting for the
/// periodic renewal check. Forces the renewal even if the current certificate is not yet in its
/// renewal window. Only the split-runtime components register it (via
/// <see cref="ComponentMtlsTransport.AddRenewal"/>), so it resolves the same
/// <see cref="ComponentRenewalContext"/> the periodic renewal service uses.
/// </summary>
[UsedImplicitly]
internal sealed class RenewComponentCertificateCommandHandler(
    ComponentRenewalContext context,
    ILogger<RenewComponentCertificateCommandHandler> logger)
    : IHandleMessages<RenewComponentCertificateCommand>
{
    public async Task Handle(RenewComponentCertificateCommand message)
    {
        logger.LogInformation("Operator-requested certificate renewal; renewing now.");

        // Serialize with the periodic renewal service so the two cannot POST /renew concurrently.
        await context.RenewalLock.WaitAsync(CancellationToken.None);
        try
        {
            await ComponentEnrollment.EnsureEnrolledAsync(
                context.Store, context.Identity, context.EndpointResolver, context.Options,
                context.TrustAnchorBundlePath, context.LoggerFactory, CancellationToken.None, force: true);
        }
        finally
        {
            context.RenewalLock.Release();
        }
    }
}
