using System.Text.Json;

namespace Eryph.Modules.HostAgent;

internal class SyncServiceCommand
{
    public string CommandName { get; set; }
    public JsonElement? Data { get; set; }
}