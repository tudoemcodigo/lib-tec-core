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

    private static string WriteTemp(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"tec-core-{Guid.NewGuid():N}.txt");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
