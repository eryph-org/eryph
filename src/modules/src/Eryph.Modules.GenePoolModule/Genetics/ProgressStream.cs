using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.GenePool.Genetics;

public class ProgressStream(
    Stream innerStream,
    TimeSpan reportingInterval,
    Func<long, CancellationToken, Task> reportProgress) : Stream
{
    private long _bytesWritten;
    private readonly Stopwatch _stopwatch = new();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => innerStream.Length;

    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override void Flush()
    {
        innerStream.Flush();
        SendProgess(CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await innerStream.FlushAsync(cancellationToken);
        await SendProgess(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stopwatch.Start();
        innerStream.Write(buffer, offset, count);
        ProcessProgress(count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _stopwatch.Start();
        await innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        await ProcessProgress(count, cancellationToken);
    }

    private async Task ProcessProgress(
        int count,
        CancellationToken cancellationToken)
    {
        _bytesWritten += count;
        if (_stopwatch.Elapsed >= reportingInterval)
            await SendProgess(cancellationToken);
    }

    private async Task SendProgess(
        CancellationToken cancellationToken)
    {
        await reportProgress(_bytesWritten, cancellationToken);
        _stopwatch.Restart();
    }
}
