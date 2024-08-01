using System;
using System.Diagnostics.CodeAnalysis;
using LanguageExt;
using Microsoft.Win32;

namespace Eryph.Core.Sys;

public interface RegistryIO
{
    [return: MaybeNull] object GetValue(string key, [AllowNull] string valueName);

    public Unit WriteValue(string key, [AllowNull] string valueName, object value);
}

public readonly struct LiveRegistryIO : RegistryIO
{
    public static readonly RegistryIO Default = new LiveRegistryIO();

    [return: MaybeNull]
    public object GetValue(string key, [AllowNull] string valueName)
    {
        return OperatingSystem.IsWindows() ? Registry.GetValue(key, valueName, null) : null;
    }

    public Unit WriteValue(string key, [AllowNull] string valueName, object value)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();
        
        Registry.SetValue(key, valueName, value);
        return default;
    }
}
