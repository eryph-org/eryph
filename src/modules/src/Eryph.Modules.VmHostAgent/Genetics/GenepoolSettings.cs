using System;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GenepoolSettings(string Name, Uri ApiEndpoint, Uri AuthEndpoint, string AuthClientId, bool IsStaging);