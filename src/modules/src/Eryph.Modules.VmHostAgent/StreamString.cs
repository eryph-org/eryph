using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent;

public class StreamString
{
    private readonly Stream _ioStream;

    public StreamString(Stream ioStream)
    {
        this._ioStream = ioStream;
    }

    public async Task<string?> ReadString(CancellationToken cancellationToken)
    {
        using var sr = new StreamReader(_ioStream, leaveOpen: true);
        return await sr.ReadLineAsync(cancellationToken);
    }

    internal Task WriteResponse(SyncServiceResponse response, CancellationToken cancellationToken)
    {
        var responseString = JsonSerializer.Serialize(response);
        using var sw = new StreamWriter(_ioStream, leaveOpen: true);
        return sw.WriteLineAsync(responseString).WaitAsync(cancellationToken);
    }

    internal Task WriteCommand(SyncServiceCommand command, CancellationToken cancellationToken)
    {
        var responseString = JsonSerializer.Serialize(command);
        using var sw = new StreamWriter(_ioStream, leaveOpen: true);
        return sw.WriteLineAsync(responseString).WaitAsync(cancellationToken);
    }

    public Task WriteString(string outString, CancellationToken cancellationToken)
    {
        using var sw = new StreamWriter(_ioStream, leaveOpen: true);
        return sw.WriteLineAsync(outString).WaitAsync(cancellationToken);
    }
}