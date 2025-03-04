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

    internal async Task WriteResponse(SyncServiceResponse response, CancellationToken cancellationToken)
    {
        var responseString = JsonSerializer.Serialize(response);
        await using var sw = new StreamWriter(_ioStream, leaveOpen: true);
        await sw.WriteLineAsync(responseString).WaitAsync(cancellationToken);
        await sw.FlushAsync(cancellationToken);
    }

    internal async Task WriteCommand(SyncServiceCommand command, CancellationToken cancellationToken)
    {
        var responseString = JsonSerializer.Serialize(command);
        await using var sw = new StreamWriter(_ioStream, leaveOpen: true);
        await sw.WriteLineAsync(responseString).WaitAsync(cancellationToken);
        await sw.FlushAsync(cancellationToken);
    }

    public async Task WriteString(string outString, CancellationToken cancellationToken)
    {
        await using var sw = new StreamWriter(_ioStream, leaveOpen: true);
        await sw.WriteLineAsync(outString).WaitAsync(cancellationToken);
        await sw.FlushAsync(cancellationToken);
    }
}
