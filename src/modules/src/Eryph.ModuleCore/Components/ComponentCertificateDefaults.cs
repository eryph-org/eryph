using System;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Shared timing for the component certificate lifecycle, so the enrollment/renewal path and the
/// certificate store agree on a single value instead of repeating it at each call site.
/// </summary>
public static class ComponentCertificateDefaults
{
    /// <summary>
    /// How long before a leaf certificate expires it is considered due for renewal. A component still
    /// connects while inside this window (the certificate is valid); it renews in the background.
    /// </summary>
    public static readonly TimeSpan RenewalLeadTime = TimeSpan.FromDays(45);
}
