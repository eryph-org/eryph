using System;

namespace Eryph.Modules.GenePool.Genetics;

public record GenePoolSettings(string Name, Uri ApiEndpoint, Uri AuthEndpoint, string AuthClientId, bool IsStaging);