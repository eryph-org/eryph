using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Eryph.ConfigModel;
using Eryph.Core.Sys;
using Eryph.GenePool.Client;
using Eryph.GenePool.Client.Credentials;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Pipes;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Genetics;

public static class GenePoolCli<RT> where RT : struct,
    HasCancel<RT>,
    HasConsole<RT>,
    HasFile<RT>,
    HasEnvironment<RT>
{
    private static readonly Uri GenePoolUri = new("https://eryphgenepoolapistaging.azurewebsites.net/api/");

    public static Aff<RT, Unit> createApiKey(
        IGenePoolApiKeyStore apiKeyStore) =>
        from _1 in Console<RT>.writeLine("Gene pool authentication")
        from _2 in Console<RT>.write("Enter your organization: ")
        from orgName in Console<RT>.readLine
        from validOrgName in OrganizationName.NewEither(orgName).ToAff()
        from _3 in Console<RT>.writeLine("Authenticating with the gene pool. Please check your browser.")
        from client in CreateGenePoolClient()
        from cancelToken in cancelToken<RT>()
        from _4 in Console<RT>.writeLine("Creating API key...")
        from hostname in Environment<RT>.machineName
        let apiKeyName = $"Eryph Zero on {hostname}"
        from result in Aff(async () =>
        {
            var response = await client.GetOrganizationClient(validOrgName)
                .CreateApiKeyAsync(apiKeyName, ["Geneset.Read"], cancelToken);

            return Optional(response);
        })
        from validResult in result.ToAff(Error.New("The API was not created"))
        let apiKey = new GenePoolApiKey()
        {
            Id = validResult.KeyId,
            Name = apiKeyName,
            Organization = validResult.Organization,
            Secret = validResult.Secret,
        }
        from _5 in apiKeyStore.SaveApiKey(GenePoolNames.EryphGenePool, apiKey).ToAff(identity)
        from _6 in Console<RT>.writeLine("API key was successfully created.")
        select unit;

    private static Aff<RT, GenePoolClient> CreateGenePoolClient() =>
        from result in Aff(async () =>
        {
            var credentialOptions = new B2CInteractiveBrowserCredentialOptions
            {
                ClientId = "56136c3f-d46e-4644-a66c-b88304d09da8",
                AuthorityUri =
                    "https://dbosoftb2cstaging.b2clogin.com/tfp/dbosoftb2cstaging.onmicrosoft.com/B2C_1A_SIGNUP_SIGNIN/v2.0",
                BrowserCustomization = new BrowserCustomizationOptions
                {
                    UseEmbeddedWebView = false,
                }
            };

            var credential = new B2CInteractiveBrowserCredential(credentialOptions);
            await credential.AuthenticateAsync(new TokenRequestContext());

            return new GenePoolClient(GenePoolUri, credential);
        })
        select result;

    public static Aff<RT, Unit> getApiKeyStatus(IGenePoolApiKeyStore genePoolApiStore) =>
        from _1 in Console<RT>.writeLine("The following gene pool API keys are configured:")
        from apiKeys in genePoolApiStore.GetApiKeys().ToAff(identity)
        from _2 in apiKeys.ToSeq()
            .Map(t => getApiKeyStatus(t.Key, t.Value))
            .SequenceSerial()
        select unit;

    private static Aff<RT, Unit> getApiKeyStatus(string poolName, GenePoolApiKey apiKey) =>
        from _1 in Console<RT>.writeLine($"Gene pool:    {poolName}")
        from _2 in Console<RT>.writeLine($"Key ID:       {apiKey.Id}")
        from _3 in Console<RT>.writeLine($"Key Name:     {apiKey.Name}")
        from _4 in Console<RT>.writeLine($"Organisation: {apiKey.Organization}")
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
        from _5 in Console<RT>.writeLine($"Valid:        {isValid}")
        select unit;
}
