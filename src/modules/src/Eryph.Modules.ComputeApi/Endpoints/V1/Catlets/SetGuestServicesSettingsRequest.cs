using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class SetGuestServicesSettingsRequest : SingleEntityRequest
{
    [FromBody]
    public required GuestServicesSettingsBody Body { get; set; }
}

public class GuestServicesSettingsBody
{
    /// <summary>
    /// Shell command for interactive SSH sessions. Null leaves it unchanged, an
    /// empty string clears the override, any other value sets it.
    /// </summary>
    public string? Shell { get; set; }

    /// <summary>
    /// Arguments for the shell command. Same null/empty/value semantics as
    /// <see cref="Shell"/>.
    /// </summary>
    public string? ShellArgs { get; set; }
}
