using System.Text.Json;

namespace Eryph.Modules.VmHostAgent;

internal class SyncServiceResponse
{
    public string Response { get; set; }
    public JsonElement? Data { get; set; }
    public string Error { get; set; }
}