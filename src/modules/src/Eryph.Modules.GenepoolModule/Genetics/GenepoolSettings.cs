using System;

namespace Eryph.Modules.Genepool.Genetics;

public record GenepoolSettings(string Name, Uri ApiEndpoint, Uri AuthEndpoint, string AuthClientId, bool IsStaging);