using System.Linq;
using System.Net;
using Azure.Core;
using Eryph.AnsiConsole.Sys;
using Eryph.ConfigModel;
using Eryph.Core.Sys;
using Eryph.GenePool.Client;
using Eryph.GenePool.Client.Credentials;
using Eryph.GenePool.Client.Requests;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Requests.User;
using Eryph.GenePool.Model.Responses;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;
using Spectre.Console;
using Spectre.Console.Rendering;
using static LanguageExt.Prelude;
using Prelude = Eryph.AnsiConsole.Prelude;

namespace Eryph.Modules.VmHostAgent.Genetics;

public static class GenePoolCli<RT> where RT : struct,
    HasAnsiConsole<RT>,
    HasApplicationInfo<RT>,
    HasCancel<RT>,
    HasEnvironment<RT>,
    HasFile<RT>,
    HasHardwareId<RT>
{
    public static Aff<RT, Unit> login(IGenePoolApiKeyStore apiKeyStore, GenepoolSettings genepoolSettings) =>
        from _1 in AnsiConsole<RT>.write(new Rows(
            new Text("We will create a dedicated API key for this machine which grants read-only access to the genepool."),
            new Text("You will need to authenticate with your personal credentials."),
            Text.Empty))
        from existingApiKey in apiKeyStore.GetApiKey(genepoolSettings.Name)
            .ToAff(identity)
        from shouldContinue in existingApiKey.Match(
            Some: apiKey =>
                from _ in AnsiConsole<RT>.markupLine(
                    "[yellow]A gene pool API key is already configured. "
                    + " When you continue, the existing API key will be overwritten.[/]")
                from shouldOverwrite in AnsiConsole<RT>.confirm("Overwrite API key?", false)
                select shouldOverwrite,
            None: () => SuccessAff<RT, bool>(true))
        from _2 in shouldContinue ? createApiKey(apiKeyStore, genepoolSettings) : unitEff
        select unit;

    public static Aff<RT, Unit> getApiKeyStatus(IGenePoolApiKeyStore genePoolApiStore, GenepoolSettings genepoolSettings) =>
        from apiKeys in genePoolApiStore.GetApiKeys().ToAff(identity)
        let apiKey = apiKeys.Find(genepoolSettings.Name)
        from _ in apiKey.Match(
            Some: key =>
                from _1 in AnsiConsole<RT>.writeLine("The following gene pool API key is configured:")
                from _2 in AnsiConsole<RT>.write(printApiKey(key))
                from _3 in validateApiKey(key, genepoolSettings)
                select unit,
            None: () => AnsiConsole<RT>.writeLine("No gene pool API key is configured."))
        select unit;

    public static Aff<RT, Unit> logout(IGenePoolApiKeyStore keyStore, GenepoolSettings genepoolSettings) =>
        from apiKeys in keyStore.GetApiKeys().ToAff(identity)
        let apiKey = apiKeys.Find(genepoolSettings.Name)
        from _ in apiKey.Match(
            Some: key =>
                from _1 in AnsiConsole<RT>.writeLine("The following gene pool API key is configured:")
                from _2 in AnsiConsole<RT>.write(printApiKey(key))
                from shouldRemove in AnsiConsole<RT>.confirm("Remove API key?", false)
                from _3 in shouldRemove
                    ? removeApiKey(keyStore, genepoolSettings, key)
                    : unitEff
                select unit,
            None: () => AnsiConsole<RT>.writeLine("No gene pool API key is configured."))
        select unit;

    private static Aff<RT, Unit> createApiKey(
        IGenePoolApiKeyStore apiKeyStore, GenepoolSettings genepoolSettings) =>
        from orgName in AnsiConsole<RT>.prompt(
            "Enter your organization:",
            OrganizationName.NewValidation)
        from hostname in Environment<RT>.machineName
        from apiKeyName in AnsiConsole<RT>.prompt(
            "Enter a name for the API key:",
            ApiKeyName.NewValidation,
            $"eryph-zero-{hostname.ToLowerInvariant()}")
        from cancelToken in cancelToken<RT>()
        from client in createInteractiveGenePoolClient(genepoolSettings)
        from stopSpinner in AnsiConsole<RT>.startSpinner("Creating API key...")
        from result in Aff(async () =>
        {
            var response = await client.GetOrganizationClient(orgName)
                .CreateApiKeyAsync(apiKeyName, ["Geneset.Read"], cancellationToken: cancelToken);
            return Optional(response);
        }) | @catch(e => stopSpinner.Bind(_ => FailAff<RT, Option<ApiKeySecretResponse>>(e)))
        from _1 in stopSpinner
        from validResult in result.ToAff(Error.New("The gene pool API key was not created."))
        let apiKey = new GenePoolApiKey()
        {
            Id = validResult.KeyId,
            Name = apiKeyName.Value,
            Organization = validResult.Organization,
            Secret = validResult.Secret,
        }
        from _2 in apiKeyStore.SaveApiKey(genepoolSettings.Name, apiKey).ToAff(identity)
        from _3 in AnsiConsole<RT>.writeLine("The gene pool API key was successfully created.")
        select unit;

    private static Aff<RT, Unit> validateApiKey(GenePoolApiKey apiKey, GenepoolSettings genepoolSettings) =>
        from userResponse in getApiKeyUser(apiKey, genepoolSettings).Map(Optional)
                            | @catch(ex => ex is GenepoolClientException { StatusCode: HttpStatusCode.Unauthorized },
                                Option<GetMeResponse>.None)
                            | @catch(_ => true, e => Error.New("Could not validate the gene pool API key.", e))
        from _ in userResponse.Match(
            Some: response =>
                from _ in AnsiConsole<RT>.write(new Rows(
                    new Markup("The gene pool API key is valid. The following information was returned by the gene pool:"),
                    new Grid()
                        .AddColumn()
                        .AddColumn()
                        .AddRow(new Text("Key ID:"), new Text(response.Id))
                        .AddRow(new Text("Key Name:"), new Text(response.DisplayName ?? ""))
                        .AddRow(new Text(response.GenepoolOrgs?.Length != 1 ? "Organizations:": "Organization:"), 
                            new Text(string.Join(',',(response.GenepoolOrgs ?? []).Select(x=>x.Name))))))
                select unit,
            None: () =>
                from _ in AnsiConsole<RT>.writeLine("The gene pool API key is invalid.")
                select unit)
        select unit;

    private static Aff<RT, GetMeResponse> getApiKeyUser(GenePoolApiKey apiKey, GenepoolSettings genepoolSettings) =>
        from cancelToken in cancelToken<RT>()
        from applicationId in ApplicationInfo<RT>.applicationId()
        from hardwareId in HardwareId<RT>.hashedHardwareId()
        from stopSpinner in AnsiConsole<RT>.startSpinner("Validating API key...")
        from response in Aff(async () =>
        {
            var client = new GenePoolClient(
                genepoolSettings.ApiEndpoint,
                new ApiKeyCredential(apiKey.Secret),
                new GenePoolClientOptions()
                {
                    Diagnostics =
                    {
                        ApplicationId = applicationId,
                    },
                    HardwareId = hardwareId,
                });
            var response = await client.GetUserClient()
                .GetAsync(new GetUserRequestOptions{Expand = new ExpandFromUser{ GenepoolOrgs = true}}, cancellationToken: cancelToken);

            return response;
        }) | @catch(e => stopSpinner.Bind(_ => FailAff<RT, GetMeResponse>(e)))
        from _ in stopSpinner
        select response;

    private static Aff<RT, Unit> removeApiKey(
        IGenePoolApiKeyStore apiKeyStore,
        GenepoolSettings genepoolSettings,
        GenePoolApiKey apiKey) =>
        from _1 in AnsiConsole<RT>.writeLine(
            "Do you want to delete the API key from the remote gene pool? "
            + "You will need to authenticate with the gene pool.")
        from shouldDelete in AnsiConsole<RT>.confirm("Delete gene pool API key from gene pool?", false)
        from shouldContinue in shouldDelete
            ? deleteApiKeyFromPool(apiKey, genepoolSettings).Map(_ => true)
              | @catch(e =>
                  from _1 in AnsiConsole<RT>.write(new Rows(
                      new Markup("[red]Could not delete the API key from the remote gene pool.[/]"),
                      Prelude.Renderable(e)))
                  from shouldContinue in AnsiConsole<RT>.confirm("Remove the gene pool API key from this machine?", false)
                  select shouldContinue)
            : SuccessAff<RT, bool>(true)
        from _2 in shouldContinue
            ? apiKeyStore.RemoveApiKey(genepoolSettings.Name).ToAff(identity)
            : unitEff
        from _5 in AnsiConsole<RT>.writeLine("The gene pool API key was successfully removed.")
        select unit;

    private static Aff<RT, Unit> deleteApiKeyFromPool(GenePoolApiKey apiKey, GenepoolSettings genepoolSettings) =>
        from client in createInteractiveGenePoolClient(genepoolSettings)
        from cancelToken in cancelToken<RT>()
        from stopSpinner in AnsiConsole<RT>.startSpinner("Deleting API key...")
        from _1 in Aff(async () =>
        {
            await client.GetApiKeyClient(apiKey.Organization, apiKey.Id)
                .DeleteAsync(cancellationToken: cancelToken);
            return unit;
        }) | @catch(e => stopSpinner.Bind(_ => FailAff<RT, Unit>(e)))
        from _2 in stopSpinner
        select unit;

    private static Aff<RT, GenePoolClient> createInteractiveGenePoolClient(GenepoolSettings genepoolSettings) =>
        from cancelToken in cancelToken<RT>()
        from applicationId in ApplicationInfo<RT>.applicationId()
        from hardwareId in HardwareId<RT>.hashedHardwareId()
        from stopSpinner in AnsiConsole<RT>.startSpinner("Authenticating with the gene pool. Please check your browser.")
        let options = new GenePoolClientOptions(GenePoolClientOptions.ServiceVersion.V1, genepoolSettings.ApiEndpoint.ToString(), genepoolSettings.IsStaging)
        {
            Diagnostics =
            {
                ApplicationId = applicationId
            },
            HardwareId = hardwareId,
        }
        from result in Aff(async () =>
        {
            var credentialOptions = new B2CInteractiveBrowserCredentialOptions
            {
                ClientId = genepoolSettings.AuthClientId,
                AuthorityUri = genepoolSettings.AuthEndpoint.AbsoluteUri,
                BrowserCustomization = new BrowserCustomizationOptions
                {
                    UseEmbeddedWebView = false,
                }
            };
            
            var credential = new B2CInteractiveBrowserCredential(credentialOptions);
            await credential.AuthenticateAsync(new TokenRequestContext());
            
            return new GenePoolClient(genepoolSettings.ApiEndpoint, credential, options);
        }) | @catch(e => stopSpinner.Bind(_ => FailAff<RT, GenePoolClient>(e)))
        from _ in stopSpinner
        from __ in AnsiConsole<RT>.writeLine("Successfully authenticated with the gene pool.")
        select result;

    private static IRenderable printApiKey(GenePoolApiKey apiKey) =>
        new Grid()
            .AddColumn()
            .AddColumn()
            .AddRow(new Text("Key ID:"), new Text(apiKey.Id))
            .AddRow(new Text("Key Name:"), new Text(apiKey.Name))
            .AddRow(new Text("Organization:"), new Text(apiKey.Organization));
}
