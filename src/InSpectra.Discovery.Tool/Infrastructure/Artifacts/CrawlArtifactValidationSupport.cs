namespace InSpectra.Discovery.Tool.Infrastructure.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class CrawlArtifactValidationSupport
{
    public const int MaxArtifactBytes = 1024 * 1024;
    private static readonly int NewLineByteCount = Encoding.UTF8.GetByteCount(Environment.NewLine);

    public static bool TryValidate(JsonObject crawlArtifact, out long byteCount, out string? validationError)
    {
        ArgumentNullException.ThrowIfNull(crawlArtifact);

        using var countingStream = new ByteCountingStream();
        JsonSerializer.Serialize(countingStream, crawlArtifact, JsonOptions.RepositoryFiles);
        byteCount = countingStream.BytesWritten + NewLineByteCount;

        return TryValidateByteCount(byteCount, out validationError);
    }

    public static bool TryValidatePath(string path, out long byteCount, out string? validationError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byteCount = new FileInfo(path).Length;
        return TryValidateByteCount(byteCount, out validationError);
    }

    public static bool TryLoadValidatedJsonObject(string path, out JsonObject? document, out string? validationError)
    {
        document = null;
        if (!TryValidatePath(path, out _, out validationError))
        {
            return false;
        }

        document = JsonNodeFileLoader.TryLoadJsonObject(path);
        return document is not null;
    }

    private static bool TryValidateByteCount(long byteCount, out string? validationError)
    {
        if (byteCount <= MaxArtifactBytes)
        {
            validationError = null;
            return true;
        }

        validationError = string.Create(
            CultureInfo.InvariantCulture,
            $"crawl.json is {byteCount:N0} bytes, which exceeds the 1 MiB limit of {MaxArtifactBytes:N0} bytes.");
        return false;
    }

    private sealed class ByteCountingStream : Stream
    {
        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;

        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => BytesWritten += count;

        public override void Write(ReadOnlySpan<byte> buffer)
            => BytesWritten += buffer.Length;

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            BytesWritten += count;
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BytesWritten += buffer.Length;
            return ValueTask.CompletedTask;
        }

        public override void WriteByte(byte value)
            => BytesWritten++;
    }
}
