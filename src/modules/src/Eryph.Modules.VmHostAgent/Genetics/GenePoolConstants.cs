using System;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal static class GenePoolConstants
{
    internal static class Local
    {
        public static readonly string Name = "local";
    }

    internal static class EryphGenePool
    {
        public static readonly string Name = "eryph-genepool";
        public static readonly Uri ApiEndpoint = new("https://eryphgenepoolapistaging.azurewebsites.net/api/");
        public static readonly Uri AuthEndpoint = new("https://dbosoftb2cstaging.b2clogin.com/tfp/dbosoftb2cstaging.onmicrosoft.com/B2C_1A_SIGNUP_SIGNIN/v2.0");
        public static readonly string AuthClientId = "56136c3f-d46e-4644-a66c-b88304d09da8";
        public static readonly Uri CdnEndpoint = new("https://eryph-staging-b2.b-cdn.net");
    }
}
