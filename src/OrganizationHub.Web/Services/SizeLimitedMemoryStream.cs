using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

internal sealed class SizeLimitedMemoryStream : MemoryStream
{
    private readonly long mMaximumBytes;

    public SizeLimitedMemoryStream(FileByteLimit limit)
    {
        mMaximumBytes = limit.GetRequiredValue();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ensureWithinLimit(Position + count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ensureWithinLimit(Position + buffer.Length);
        base.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ensureWithinLimit(Position + count);
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ensureWithinLimit(Position + buffer.Length);
        return base.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        ensureWithinLimit(Position + 1);
        base.WriteByte(value);
    }

    public override void SetLength(long value)
    {
        ensureWithinLimit(value);
        base.SetLength(value);
    }

    private void ensureWithinLimit(long length)
    {
        if (length > mMaximumBytes)
        {
            throw new FileSizeLimitExceededException();
        }
    }
}
