using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Cryptography.Hybrid;
using TEC.Core.Cryptography.Symmetric;

namespace TEC.Core.Benchmarks;

/// <summary>AES-GCM (memória e stream), hash, HMAC e comparação em tempo constante.</summary>
[MemoryDiagnoser]
public class SymmetricBenchmarks
{
    private readonly AesGcmCryptography _aes = new();
    private byte[] _key = [];
    private byte[] _data = [];
    private byte[] _encrypted = [];
    private string _hashA = string.Empty;
    private string _hashB = string.Empty;

    [Params(1_024, 1_048_576)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _key = _aes.GenerateKey();
        _data = RandomNumberGenerator.GetBytes(Size);
        _encrypted = _aes.Encrypt(_data, _key);
        _hashA = HashHelper.ComputeHash("a");
        _hashB = HashHelper.ComputeHash("b");
    }

    [Benchmark]
    public byte[] AesEncrypt() => _aes.Encrypt(_data, _key);

    [Benchmark]
    public byte[] AesDecrypt() => _aes.Decrypt(_encrypted, _key);

    [Benchmark]
    public async Task<long> AesEncryptStream()
    {
        using var output = new MemoryStream(Size + 1024);
        await _aes.EncryptAsync(new MemoryStream(_data, writable: false), output, _key);
        return output.Length;
    }

    [Benchmark]
    public byte[] Sha256() => HashHelper.ComputeHash(_data);

    [Benchmark]
    public byte[] HmacSha256() => HashHelper.ComputeHmac(_data, _key);

    [Benchmark]
    public bool FixedTimeEquals() => HashHelper.FixedTimeEquals(_hashA, _hashB);
}

/// <summary>RSA (assinatura e cifra) e criptografia híbrida.</summary>
[MemoryDiagnoser]
public class AsymmetricBenchmarks
{
    private readonly RsaCryptography _rsa = new();
    private readonly HybridCryptography _hybrid = new();
    private RsaKeyPair _keys = null!;
    private byte[] _data = [];
    private byte[] _signature = [];
    private byte[] _hybridEncrypted = [];

    [GlobalSetup]
    public void Setup()
    {
        _keys = _rsa.GenerateKeyPair(2048);
        _data = RandomNumberGenerator.GetBytes(4096);
        _signature = _rsa.SignData(_data, _keys.PrivateKeyPem);
        _hybridEncrypted = _hybrid.Encrypt(_data, _keys.PublicKeyPem);
    }

    [Benchmark]
    public byte[] RsaSign() => _rsa.SignData(_data, _keys.PrivateKeyPem);

    [Benchmark]
    public bool RsaVerify() => _rsa.VerifyData(_data, _signature, _keys.PublicKeyPem);

    [Benchmark]
    public byte[] HybridEncrypt() => _hybrid.Encrypt(_data, _keys.PublicKeyPem);

    [Benchmark]
    public byte[] HybridDecrypt() => _hybrid.Decrypt(_hybridEncrypted, _keys.PrivateKeyPem);
}

/// <summary>
/// PBKDF2: custo de uma verificação de senha (define quantos logins por segundo cada núcleo atende).
/// Verificar com hash nulo (usuário inexistente) deve custar o mesmo que com hash real.
/// </summary>
[MemoryDiagnoser]
public class PasswordBenchmarks
{
    private Pbkdf2PasswordHasher _hasher = null!;
    private string _hash = string.Empty;

    [Params(100_000, Pbkdf2PasswordHasher.DefaultIterations)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _hasher = new Pbkdf2PasswordHasher(Iterations);
        _hash = _hasher.Hash("senha correta");
    }

    [Benchmark(Baseline = true)]
    public bool VerifyExistingUser() => _hasher.Verify("senha errada", _hash);

    [Benchmark]
    public bool VerifyUnknownUser() => _hasher.Verify("senha errada", null);
}
