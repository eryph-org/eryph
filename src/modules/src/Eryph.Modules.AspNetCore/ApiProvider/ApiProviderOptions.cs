namespace Eryph.Modules.AspNetCore.ApiProvider;

public class ApiProviderOptions
{
    public required string ApiName { get; set; }

    public ApiProviderOAuthOptions? OAuthOptions { get; set; }
}
