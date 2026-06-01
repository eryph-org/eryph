using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Components;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Decides whether a component enrollment request is authorized to receive a certificate. This is
/// the seam the out-of-repo distro / enterprise tooling replaces (host attestation, secret store,
/// cloud instance identity, …). The in-repo default validates an operator-minted one-time enrollment
/// token. Issuance itself (the CA) is always in-repo; only this authorization decision is pluggable.
/// </summary>
public interface IComponentEnrollmentPolicy
{
    Task<bool> IsAuthorizedAsync(ComponentEnrollmentRequest request, CancellationToken cancellationToken = default);
}
