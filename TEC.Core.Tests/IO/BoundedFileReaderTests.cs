using System.Text;
using TEC.Core.IO;
using TEC.Core.Text.Masking;

namespace TEC.Core.Tests.IO;

public class BoundedFileReaderTests
{
    [Test]
    public async Task Reads_utf8_and_drops_bom()
    {
        string path = WriteTemp([0xEF, 0xBB, 0xBF, .. "segredo-ç"u8]);
        try
        {
            await Assert.That(BoundedFileReader.TryReadUtf8(path, 1024, out string? text)).IsTrue();
            await Assert.That(text).IsEqualTo("segredo-ç");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task File_exactly_at_limit_is_read_and_one_byte_above_is_refused()
    {
        string atLimit = WriteTemp(Encoding.ASCII.GetBytes(new string('a', 100)));
        string above = WriteTemp(Encoding.ASCII.GetBytes(new string('a', 101)));
        try
        {
            await Assert.That(BoundedFileReader.TryReadUtf8(atLimit, 100, out string? text)).IsTrue();
            await Assert.That(text!.Length).IsEqualTo(100);
            await Assert.That(BoundedFileReader.TryReadUtf8(above, 100, out string? refused)).IsFalse();
            await Assert.That(refused).IsNull();
        }
        finally
        {
            File.Delete(atLimit);
            File.Delete(above);
        }
    }

    [Test]
    public async Task Large_file_grows_buffer_correctly()
    {
        string content = string.Concat(Enumerable.Repeat("0123456789", 5_000));   // 50 KB: várias ampliações do buffer
        string path = WriteTemp(Encoding.ASCII.GetBytes(content));
        try
        {
            await Assert.That(BoundedFileReader.TryReadUtf8(path, 1024 * 1024, out string? text)).IsTrue();
            await Assert.That(text).IsEqualTo(content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Invalid_utf8_throws_without_leaking_content()
    {
        string path = WriteTemp([0x73, 0x65, 0x6E, 0x68, 0x61, 0xFF, 0xFE]);   // "senha" + bytes inválidos
        try
        {
            var exception = await Assert.That(() => BoundedFileReader.TryReadUtf8(path, 1024, out _)).Throws<InvalidDataException>();
            await Assert.That(exception!.Message).DoesNotContain("senha");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Invalid_arguments_are_rejected()
    {
        await Assert.That(() => BoundedFileReader.TryReadUtf8("", 10, out _)).Throws<ArgumentException>();
        await Assert.That(() => BoundedFileReader.TryReadUtf8("x", 0, out _)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DescribeUntrusted_never_reproduces_the_text_and_correlates_same_input()
    {
        const string secret = "sk-live-abc123";
        string first = SensitiveDataMasker.DescribeUntrusted(secret);
        string second = SensitiveDataMasker.DescribeUntrusted(secret);
        string other = SensitiveDataMasker.DescribeUntrusted(secret + "x");

        await Assert.That(first).DoesNotContain("abc123");
        await Assert.That(first).StartsWith("<14 caracteres, hmac:");
        await Assert.That(first).IsEqualTo(second);
        await Assert.That(other).IsNotEqualTo(first);
        await Assert.That(SensitiveDataMasker.DescribeUntrusted(null)).IsEqualTo("<nulo>");
        await Assert.That(SensitiveDataMasker.DescribeUntrusted("")).IsEqualTo("<vazio>");
    }

    [Test]
    public async Task MaxBytes_at_int_max_value_does_not_overflow()
    {
        // R08: maxBytes + 1 estourava (OverflowException) com int.MaxValue
        string path = WriteTemp("abc"u8.ToArray());
        try
        {
            await Assert.That(BoundedFileReader.TryReadUtf8(path, int.MaxValue, out string? text)).IsTrue();
            await Assert.That(text).IsEqualTo("abc");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---------- TryReadBytesAsync (leitura binária usada por DecryptFromFileAsync) ----------

    [Test]
    public async Task TryReadBytesAsync_at_limit_is_read_and_one_byte_above_is_refused()
    {
        byte[] content = [0x00, 0xFF, 0xC3, 0x28, .. Enumerable.Range(0, 96).Select(i => (byte)i)];   // binário, UTF-8 inválido
        string atLimit = WriteTemp(content);
        string above = WriteTemp([.. content, 0x01]);
        try
        {
            var read = await BoundedFileReader.TryReadBytesAsync(atLimit, 100, CancellationToken.None);
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.SequenceEqual(content)).IsTrue();
            await Assert.That(await BoundedFileReader.TryReadBytesAsync(above, 100, CancellationToken.None)).IsNull();
        }
        finally
        {
            File.Delete(atLimit);
            File.Delete(above);
        }
    }

    [Test]
    public async Task TryReadBytesAsync_empty_file_returns_empty_array()
    {
        string path = WriteTemp([]);
        try
        {
            var read = await BoundedFileReader.TryReadBytesAsync(path, 10, CancellationToken.None);
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.Length).IsEqualTo(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task TryReadBytesAsync_large_file_grows_buffer_and_keeps_bom()
    {
        byte[] content = [0xEF, 0xBB, 0xBF, .. Enumerable.Range(0, 50_000).Select(i => (byte)(i * 31))];
        string path = WriteTemp(content);
        try
        {
            var read = await BoundedFileReader.TryReadBytesAsync(path, int.MaxValue, CancellationToken.None);
            await Assert.That(read!.SequenceEqual(content)).IsTrue();   // binário: o BOM não é descartado
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task TryReadBytesAsync_file_far_above_limit_is_refused()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tec-core-{Guid.NewGuid():N}.bin");
        try
        {
            await using (var stream = File.Create(path))
                stream.SetLength(64L * 1024 * 1024);

            await Assert.That(await BoundedFileReader.TryReadBytesAsync(path, 1024, CancellationToken.None)).IsNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task TryReadBytesAsync_invalid_arguments_and_cancellation()
    {
        string path = WriteTemp("abc"u8.ToArray());
        try
        {
            await Assert.That(async () => await BoundedFileReader.TryReadBytesAsync("", 10, CancellationToken.None)).Throws<ArgumentException>();
            await Assert.That(async () => await BoundedFileReader.TryReadBytesAsync(path, 0, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(async () => await BoundedFileReader.TryReadBytesAsync(path, 10, new CancellationToken(canceled: true)))
                .Throws<OperationCanceledException>();
            await Assert.That(async () => await BoundedFileReader.TryReadBytesAsync(path + ".inexistente", 10, CancellationToken.None))
                .Throws<FileNotFoundException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTemp(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"tec-core-{Guid.NewGuid():N}.txt");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
