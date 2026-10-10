using Grpc.Core;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.UI.Shared;

/// <summary>
/// A server-streaming download read as a plain forward-only <see cref="Stream"/>, so a host can hand it to
/// whatever saves files without knowing anything about gRPC. The first message is read when the stream is
/// opened: a refusal (unknown or deleted document) therefore surfaces there and not while saving.
/// </summary>
internal sealed class DownloadStream : Stream
{
    private readonly AsyncServerStreamingCall<DownloadDocumentResponse> call;
    private readonly long length;
    private ReadOnlyMemory<byte> pending;
    private bool finished;

    private DownloadStream(AsyncServerStreamingCall<DownloadDocumentResponse> call, long length)
    {
        this.call = call;
        this.length = length;
    }

    public static async Task<DownloadStream> OpenAsync(
        AsyncServerStreamingCall<DownloadDocumentResponse> call,
        long length,
        CancellationToken cancellation
    )
    {
        var stream = new DownloadStream(call, length);

        try
        {
            await stream.FillAsync(cancellation);
        }
        catch
        {
            stream.Dispose();

            throw;
        }

        return stream;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    // Known from the document, so a consumer that wants a size gets one; the stream itself still is not seekable.
    public override long Length => length;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (pending.IsEmpty)
        {
            if (finished)
            {
                return 0;
            }

            await FillAsync(cancellationToken);
        }

        var count = Math.Min(buffer.Length, pending.Length);
        pending[..count].CopyTo(buffer);
        pending = pending[count..];

        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            call.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task FillAsync(CancellationToken cancellation)
    {
        if (await call.ResponseStream.MoveNext(cancellation))
        {
            pending = call.ResponseStream.Current.Chunk.Memory;
        }
        else
        {
            finished = true;
        }
    }
}
