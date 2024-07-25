using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Eryph.AnsiConsole.Sys;
using Eryph.ConfigModel;
using Eryph.Core.Sys;
using Eryph.GenePool.Client;
using Eryph.GenePool.Client.Credentials;
using Eryph.GenePool.Model.Requests;
using Eryph.GenePool.Model.Responses;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Pipes;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;

using static LanguageExt.Prelude;

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

    public static Aff<RT, Unit> createApiKey(
        IGenePoolApiKeyStore apiKeyStore) =>
        from _1 in AnsiConsole<RT>.writeLine("Gene pool login")
        from existingApiKey in apiKeyStore.GetApiKey(GenePoolNames.EryphGenePool)
            .ToAff(identity)
        from shouldContinue in existingApiKey.Match(
            Some: apiKey =>
                from _ in AnsiConsole<RT>.writeLine("An API key has already been created. "
                    + " When you continue, the existing API will be overwritten. The existing API key will not be invalidated.")
                from shouldOverwrite in AnsiConsole<RT>.confirm("Overwrite API key?", false)
                select shouldOverwrite,
            None: () => SuccessAff<RT, bool>(true))
        from _2 in shouldContinue
            ? from orgName in AnsiConsole<RT>.prompt(
            "Enter your organization:",
            OrganizationName.NewValidation)
        from _3 in AnsiConsole<RT>.writeLine("Authenticating with the gene pool. Please check your browser.")
        from client in createGenePoolClient()
        from cancelToken in cancelToken<RT>()
        from hostname in Environment<RT>.machineName
        let apiKeyName = $"eryph-zero-{hostname}"
        from stopSpinner in AnsiConsole<RT>.startSpinner("Creating API key...")
        from result in Aff(async () =>
        {
            var response = await client.GetOrganizationClient(orgName)
                .CreateApiKeyAsync(apiKeyName, ["Geneset.Read"], cancelToken);
            return Optional(response);
        })
        from _4 in stopSpinner
        from validResult in result.ToAff(Error.New("The API was not created"))
        let apiKey = new GenePoolApiKey()
        {
            Id = validResult.KeyId,
            Name = apiKeyName,
            Organization = validResult.Organization,
            Secret = validResult.Secret,
        }
        from _5 in apiKeyStore.SaveApiKey(GenePoolNames.EryphGenePool, apiKey).ToAff(identity)
        from _6 in AnsiConsole<RT>.writeLine("API key was successfully created.")
              select unit
            : unitEff
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
        })
        from _ in stopSpinner
        from __ in AnsiConsole<RT>.writeLine("Successfully authenticated with the gene pool.")
        select result;

    public static Aff<RT, Unit> getApiKeyStatus(IGenePoolApiKeyStore genePoolApiStore) =>
        from _1 in AnsiConsole<RT>.writeLine("The following gene pool API keys are configured:")
        from apiKeys in genePoolApiStore.GetApiKeys().ToAff(identity)
        from _2 in apiKeys.ToSeq()
            .Map(t => getApiKeyStatus(t.Key, t.Value))
            .SequenceSerial()
        select unit;

    private static Aff<RT, Unit> getApiKeyStatus(string poolName, GenePoolApiKey apiKey) =>
        from _1 in AnsiConsole<RT>.writeLine($"Gene pool:    {poolName}")
        from _2 in AnsiConsole<RT>.writeLine($"Key ID:       {apiKey.Id}")
        from _3 in AnsiConsole<RT>.writeLine($"Key Name:     {apiKey.Name}")
        from _4 in AnsiConsole<RT>.writeLine($"Organisation: {apiKey.Organization}")
        from cancelToken in cancelToken<RT>()
        from isValid in Aff(async () =>
        {
            var client = new GenePoolClient(GenePoolUri, new ApiKeyCredential(apiKey.Secret));
            await client.GetApiKeyClient(apiKey.Organization, apiKey.Id)
                .GetAsync(cancelToken);

            return true;
        })
        | @catch(ex => ex is ErrorResponseException { Response.StatusCode: HttpStatusCode.Unauthorized }, false)
        | @catch(_ => true, e => Error.New("´Could not check if the API is valid.", e))
        from _5 in AnsiConsole<RT>.writeLine($"Valid:        {isValid}")
        select unit;
}
