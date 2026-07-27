using Android.Media;
using IOStream = System.IO.Stream;

namespace MediaForge.Universal.Platforms.Android.Services;

internal sealed class StreamMediaDataSource : MediaDataSource
{
    private readonly IOStream _stream;
    private readonly object _sync = new();

    public StreamMediaDataSource(IOStream stream)
    {
        if (!stream.CanSeek)
        {
            throw new NotSupportedException("Metadata requires a seekable media stream.");
        }

        _stream = stream;
    }

    public override long Size => _stream.Length;

    public override int ReadAt(long position, byte[]? buffer, int offset, int size)
    {
        if (buffer is null || position < 0)
        {
            return -1;
        }

        lock (_sync)
        {
            _stream.Seek(position, SeekOrigin.Begin);
            var bytesRead = _stream.Read(buffer, offset, size);
            return bytesRead == 0 ? -1 : bytesRead;
        }
    }

    public override void Close()
    {
        // The caller owns the stream and disposes it after the Android reader.
    }
}
