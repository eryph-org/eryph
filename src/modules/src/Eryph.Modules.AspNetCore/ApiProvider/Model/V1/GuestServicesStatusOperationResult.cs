namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class GuestServicesStatusOperationResult : OperationResult
{
    public string? GuestServicesStatus { get; set; }

    public string? GuestServicesVersion { get; set; }

    public string? ProvisioningState { get; set; }

    public string? Shell { get; set; }

    public string? ShellArgs { get; set; }
}
