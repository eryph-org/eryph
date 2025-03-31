namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class ApiVersionResponse
{
    public required ApiVersion Version { get; init; } = new()
    {
        Major = 1,
        Minor = 1,
    };
}
