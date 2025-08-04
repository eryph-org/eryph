namespace Eryph.Modules.GenePool.Genetics;

public static class GenePoolConstants
{
    internal static class Local
    {
        public static readonly string Name = "local";
    }

    public const string PartClientName = "gene_part_client";

    public static GenePoolSettings ProductionGenePool => new (
        "eryph-genepool",
        new("https://genepool-api.eryph.io/"),
        new("https://login.dbosoft.eu/tfp/a18f6025-dca7-463e-b38a-84cf9f2ca684/B2C_1A_SIGNUP_SIGNIN/v2.0"),
        "0cadd98d-1e87-467b-a908-db1e340e9049", false);

    public static GenePoolSettings StagingGenePool => new(
        "eryph-genepool-staging",
        new("https://eryphgenepoolapistaging.azurewebsites.net/"),
        new("https://dbosoftb2cstaging.b2clogin.com/tfp/dbosoftb2cstaging.onmicrosoft.com/B2C_1A_SIGNUP_SIGNIN/v2.0"),
        "56136c3f-d46e-4644-a66c-b88304d09da8", true);


}