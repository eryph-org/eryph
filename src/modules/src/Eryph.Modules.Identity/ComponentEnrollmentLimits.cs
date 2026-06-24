namespace Eryph.Modules.Identity;

/// <summary>
/// Bounds on a component enrollment/renewal request that are enforced in more than one place — the
/// endpoint request-shape validation (<see cref="ComponentEnrollmentValidations"/>) and the enrollment
/// service's own defensive checks. Defined once here so the two cannot drift apart.
/// </summary>
internal static class ComponentEnrollmentLimits
{
    /// <summary>The maximum number of server DNS names (SAN entries) a server certificate may request.</summary>
    public const int MaxServerDnsNames = 16;
}
