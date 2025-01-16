using System;

namespace Eryph.VmManagement.Data.Core;

public class VMFirmwareInfo
{
    public IPProtocolPreference PreferredNetworkBootProtocol { get; init; }

    public OnOffState SecureBoot { get; init; }

    public string SecureBootTemplate { get; init; }

    public Guid? SecureBootTemplateId { get; init; }

    public ConsoleModeType ConsoleMode { get; init; }

    public OnOffState PauseAfterBootFailure { get; init; }
}
