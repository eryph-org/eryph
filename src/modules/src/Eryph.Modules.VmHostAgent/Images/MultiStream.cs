using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Eryph.Modules.VmHostAgent.Images;

internal class MultiStream : Stream
{
    private readonly Stream[] _streams;
    private long _position;
    private int _currentStreamIndex = 0;
    private long _length = 0;

    public MultiStream(IEnumerable<Stream> streams)
    {
        _streams = streams.ToArray();

        foreach (var stream in _streams)
        {
            _length += stream.Length;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _currentStreamIndex = 0;
                _position = 0;
                _streams[_currentStreamIndex].Seek(0, SeekOrigin.Begin);
                break;
            case SeekOrigin.Current:
                break;
            case SeekOrigin.End:
                _currentStreamIndex = _streams.Length - 1;
                _streams[_currentStreamIndex].Seek(0, SeekOrigin.End);

                _position = _length;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        var offsetLeft = offset;
        var seekBack = offsetLeft < 0;

        if (seekBack)
        {
            while (offsetLeft < 0)
            {
                var currentStream = _streams[_currentStreamIndex];
                if (currentStream.Length < (offsetLeft)*-1)
                {
                    offsetLeft += currentStream.Length;
                    _position -= currentStream.Length;
                    _currentStreamIndex--;
                    _streams[_currentStreamIndex].Seek(0, SeekOrigin.End);
                    continue;
                }

                currentStream.Seek(offsetLeft, SeekOrigin.Current);
                _position += offsetLeft;
                offsetLeft = 0;

            }
        }
        else
        {
            while (offsetLeft > 0)
            {
                var currentStream = _streams[_currentStreamIndex];
                if (currentStream.Length < offsetLeft)
                {
                    offsetLeft -= currentStream.Length;
                    _position += currentStream.Length;
                    _currentStreamIndex++;
                    _streams[_currentStreamIndex].Seek(0, SeekOrigin.Begin);
                    continue;
                }

                currentStream.Seek(offsetLeft, SeekOrigin.Current);
                _position += offsetLeft;
                offsetLeft = 0;

            }
        }

        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var result = 0;
        var position = offset;

        while (count > 0)
        {
            var currentStream = _streams[_currentStreamIndex];
            var bytesRead = currentStream.Read(buffer, position, count);
            result += bytesRead;
            position += bytesRead;
            _position += bytesRead;

            if (bytesRead <= count)
                count -= bytesRead;

            if (count <= 0) continue;


            if (_currentStreamIndex == _streams.Length-1)
                break;

            _currentStreamIndex++;
            _streams[_currentStreamIndex].Seek(0, SeekOrigin.Begin);
        }

        return result;
    }


    public override async System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var result = 0;
        var position = offset;

        while (count > 0)
        {
            var currentStream = _streams[_currentStreamIndex];

            var bytesRead = await currentStream.ReadAsync(buffer, position, count, cancellationToken);
            result += bytesRead;
            position += bytesRead;
            _position += bytesRead;

            if (bytesRead <= count)
                count -= bytesRead;

            if (count <= 0) continue;

            if (_currentStreamIndex == _streams.Length - 1)
                break;

            _currentStreamIndex++;
            _streams[_currentStreamIndex].Seek(0, SeekOrigin.Begin);

        }

        return result;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new InvalidOperationException();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var stream in _streams)
        {
            stream.Dispose();
        }

    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

}