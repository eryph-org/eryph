using System;

namespace Eryph.Core;

/// <summary>
/// Provides the AutoMapper license key used by every <c>MapperConfiguration</c>
/// in eryph. The key is read from the <c>ERYPH_AUTOMAPPER_LICENSE</c> environment
/// variable. When the variable is not set, AutoMapper falls back to its Community
/// behavior and emits warning logs on startup; it does not block runtime mapping.
/// </summary>
public static class AutoMapperLicense
{
    public const string EnvironmentVariable = "ERYPH_AUTOMAPPER_LICENSE";

    public static string? Key =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } key
            ? key
            : null;
}
