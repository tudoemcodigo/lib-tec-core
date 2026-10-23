using System.Security.Cryptography;

namespace TEC.Core.LoadTests.Infrastructure;

/// <summary>
/// Stream somente leitura com bytes pseudoaleatórios gerados sob demanda (sem ocupar memória), de tamanho fixo.
/// Calcula o SHA-256 do que foi lido, para comparar com a saída do processamento.
/// </summary>
public sealed class GeneratedStream(long length, ulong seed = 0x9E3779B97F4A7C15) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private ulong _state = seed;
    private long _position;

    /// <summary>SHA-256 de todos os bytes já lidos.</summary>
    public byte[] GetHash() => _hash.GetCurrentHash();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int count = (int)Math.Min(buffer.Length, length - _position);
        for (int i = 0; i < count; i++)
        {
            // xorshift64: rápido e determinístico (não é criptográfico; só gera conteúdo de teste)
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            buffer[i] = (byte)_state;
        }

        _hash.AppendData(buffer[..count]);
        _position += count;
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer, offset, count));

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hash.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Stream somente escrita que descarta os dados e calcula o SHA-256 e o tamanho do que recebeu.</summary>
public sealed class HashingSinkStream : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    /// <summary>Bytes recebidos.</summary>
    public long BytesWritten { get; private set; }

    /// <summary>SHA-256 de tudo o que foi escrito.</summary>
    public byte[] GetHash() => _hash.GetCurrentHash();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => BytesWritten;
    public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hash.AppendData(buffer);
        BytesWritten += buffer.Length;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hash.Dispose();
        base.Dispose(disposing);
    }
}
