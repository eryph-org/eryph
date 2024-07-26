using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Eryph.AnsiConsole;
using Eryph.AnsiConsole.Sys;
using Eryph.ConfigModel;
using Eryph.Core.Sys;
using Eryph.GenePool.Client;
using Eryph.GenePool.Client.Credentials;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Requests;
using Eryph.GenePool.Model.Responses;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Pipes;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;
using Spectre.Console;
using Spectre.Console.Rendering;
using static LanguageExt.Prelude;
using Prelude = Eryph.AnsiConsole.Prelude;

namespace Eryph.Modules.VmHostAgent.Genetics;

public static class GenePoolCli<RT> where RT : struct,
    HasAnsiConsole<RT>,
    HasCancel<RT>,
    HasFile<RT>,
    HasEnvironment<RT>
{
    private static readonly Uri GenePoolUri = new("https://eryphgenepoolapistaging.azurewebsites.net/api/");

    private static readonly string AuthorityUri =
        "https://dbosoftb2cstaging.b2clogin.com/tfp/dbosoftb2cstaging.onmicrosoft.com/B2C_1A_SIGNUP_SIGNIN/v2.0";

    public static Aff<RT, Unit> login(IGenePoolApiKeyStore apiKeyStore) =>
        from _1 in AnsiConsole<RT>.write(new Rows(
            new Text("We will create a dedicated API key for this machine which grants read-only access to the genepool."),
            new Text("You will need to authenticate with your personal credentials."),
            Text.Empty))
        from existingApiKey in apiKeyStore.GetApiKey(GenePoolNames.EryphGenePool)
            .ToAff(identity)
        from shouldContinue in existingApiKey.Match(
            Some: apiKey =>
                from _ in AnsiConsole<RT>.markupLine(
                    "[yellow]A gene pool API key is already configured. "
                    + " When you continue, the existing API key will be overwritten.[/]")
                from shouldOverwrite in AnsiConsole<RT>.confirm("Overwrite API key?", false)
                select shouldOverwrite,
            None: () => SuccessAff<RT, bool>(true))
        from _2 in shouldContinue ? createApiKey(apiKeyStore) : unitEff
        select unit;

    public static Aff<RT, Unit> getApiKeyStatus(IGenePoolApiKeyStore genePoolApiStore) =>
        from apiKeys in genePoolApiStore.GetApiKeys().ToAff(identity)
        let apiKey = apiKeys.Find(GenePoolNames.EryphGenePool)
        from _ in apiKey.Match(
            Some: key =>
                from _1 in AnsiConsole<RT>.writeLine("The following gene pool API key is configured:")
                from _2 in AnsiConsole<RT>.write(printApiKey(key))
                from _3 in validateApiKey(key)
                select unit,
            None: () => AnsiConsole<RT>.writeLine("No gene pool API key is configured."))
        select unit;

    public static Aff<RT, Unit> logout(IGenePoolApiKeyStore keyStore) =>
        from apiKeys in keyStore.GetApiKeys().ToAff(identity)
        let apiKey = apiKeys.Find(GenePoolNames.EryphGenePool)
        from _ in apiKey.Match(
            Some: key =>
                from _1 in AnsiConsole<RT>.writeLine("The following gene pool API key is configured:")
                from _2 in AnsiConsole<RT>.write(printApiKey(key))
                from shouldRemove in AnsiConsole<RT>.confirm("Remove API key?", false)
                from _3 in shouldRemove
                    ? removeApiKey(keyStore, GenePoolNames.EryphGenePool, key)
                    : unitEff
                select unit,
            None: () => AnsiConsole<RT>.writeLine("No gene pool API key is configured."))
        select unit;

    private static Aff<RT, Unit> createApiKey(
        IGenePoolApiKeyStore apiKeyStore) =>
        from orgName in AnsiConsole<RT>.prompt(
            "Enter your organization:",
            OrganizationName.NewValidation)
        from hostname in Environment<RT>.machineName
        from apiKeyName in AnsiConsole<RT>.prompt(
            "Enter a name for the API key:",
            ApiKeyName.NewValidation,
            $"eryph-zero-{hostname}")
        from cancelToken in cancelToken<RT>()
        from client in createGenePoolClient()
        from stopSpinner in AnsiConsole<RT>.startSpinner("Creating API key...")
        from result in Aff(async () =>
        {
            var response = await client.GetOrganizationClient(orgName)
                // TODO Remove Org.Read permission when the /me endpoint is available
                .CreateApiKeyAsync(apiKeyName, ["Geneset.Read", "Org.Read"], cancelToken);
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
        from _2 in apiKeyStore.SaveApiKey(GenePoolNames.EryphGenePool, apiKey).ToAff(identity)
        from _3 in AnsiConsole<RT>.writeLine("The gene pool API key was successfully created.")
        select unit;

    private static Aff<RT, Unit> validateApiKey(GenePoolApiKey apiKey) =>
        from keyResponse in getApiKeyFromPool(apiKey).Map(Optional)
                            | @catch(ex => ex is ErrorResponseException { Response.StatusCode: HttpStatusCode.Unauthorized },
                                Option<ApiKeyResponse>.None)
                            | @catch(_ => true, e => Error.New("Could not validate the gene pool API key.", e))
        from _ in keyResponse.Match(
            Some: response =>
                from _ in AnsiConsole<RT>.write(new Rows(
                    new Markup("The gene pool API key is valid. The following information was returned by the gene pool:"),
                    new Grid()
                        .AddColumn()
                        .AddColumn()
                        .AddRow(new Text("Key ID:"), new Text(response.KeyId))
                        .AddRow(new Text("Key Name:"), new Text(response.Name))
                        .AddRow(new Text("Organisation:"), new Text(response.Organization.Name))))
                select unit,
            None: () =>
                from _ in AnsiConsole<RT>.writeLine("The gene pool API key is invalid.")
                select unit)
        select unit;

    private static Aff<RT, ApiKeyResponse> getApiKeyFromPool(GenePoolApiKey apiKey) =>
        from cancelToken in cancelToken<RT>()
        from stopSpinner in AnsiConsole<RT>.startSpinner("Validating API key...")
        from response in Aff(async () =>
        {
            var client = new GenePoolClient(GenePoolUri, new ApiKeyCredential(apiKey.Secret));
            var response = await client.GetApiKeyClient(apiKey.Organization, apiKey.Id)
                .GetAsync(cancelToken);

            return response;
        }) | @catch(e => stopSpinner.Bind(_ => FailAff<RT, ApiKeyResponse>(e)))
        from _ in stopSpinner
        select response;

    private static Aff<RT, Unit> removeApiKey(
        IGenePoolApiKeyStore apiKeyStore,
        string genePoolName,
        GenePoolApiKey apiKey) =>
        from _1 in AnsiConsole<RT>.writeLine(
            "Do you want to delete the API key from the remote gene pool? "
            + "You will need to authenticate with the gene pool.")
        from shouldDelete in AnsiConsole<RT>.confirm("Delete gene pool API key from gene pool?", false)
        from shouldContinue in shouldDelete
            ? deleteApiKeyFromPool(apiKey).Map(_ => true)
              | @catch(e =>
                  from _1 in AnsiConsole<RT>.write(new Rows(
                      new Markup("[red]Could not delete the API key from the remote gene pool.[/]"),
                      Prelude.Renderable(e)))
                  from shouldContinue in AnsiConsole<RT>.confirm("Remove the gene pool API key from this machine?", false)
                  select shouldContinue)
            : SuccessAff<RT, bool>(true)
        from _2 in shouldContinue
            ? apiKeyStore.RemoveApiKey(genePoolName).ToAff(identity)
            : unitEff
        from _5 in AnsiConsole<RT>.writeLine("The gene pool API key was successfully removed.")
        select unit;

    private static Aff<RT, Unit> deleteApiKeyFromPool(GenePoolApiKey apiKey) =>
        from client in createGenePoolClient()
        from cancelToken in cancelToken<RT>()
        from stopSpinner in AnsiConsole<RT>.startSpinner("Deleting API key...")
        from _1 in Aff(async () =>
        {
            await client.GetApiKeyClient(apiKey.Organization, apiKey.Id)
                .DeleteAsync(cancelToken);
            return unit;
        }) | @catch(e => stopSpinner.Bind(_ => FailAff<RT, Unit>(e)))
        from _2 in stopSpinner
        select unit;

    private static Aff<RT, GenePoolClient> createGenePoolClient() =>
        from cancelToken in cancelToken<RT>()
        from stopSpinner in AnsiConsole<RT>.startSpinner("Authenticating with the gene pool. Please check your browser.")
        from result in Aff(async () =>
        {
            var credentialOptions = new B2CInteractiveBrowserCredentialOptions
            {
                ClientId = "56136c3f-d46e-4644-a66c-b88304d09da8",
                AuthorityUri = AuthorityUri,
                BrowserCustomization = new BrowserCustomizationOptions
                {
                    UseEmbeddedWebView = false,
                }
            };
            
            var credential = new B2CInteractiveBrowserCredential(credentialOptions);
            await credential.AuthenticateAsync(new TokenRequestContext());
            
            return new GenePoolClient(GenePoolUri, credential);
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
            .AddRow(new Text("Organisation:"), new Text(apiKey.Organization));
}
