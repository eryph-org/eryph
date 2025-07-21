using LanguageExt;

namespace Eryph.VmManagement.Wmi;

/// <summary>
/// Holds information extracted from a
/// <see cref="System.Management.ManagementBaseObject"/>.
/// </summary>
public record WmiObject(HashMap<string, Option<object>> Properties);
