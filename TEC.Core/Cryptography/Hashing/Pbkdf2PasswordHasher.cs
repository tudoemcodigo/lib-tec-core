using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TEC.Core.Common.Guards;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Text.Internal;

namespace TEC.Core.Cryptography.Hashing;

/// <summary>
/// Hash de senhas com PBKDF2-SHA256 (nativo do .NET).
/// </summary>
/// <remarks>
/// <para>Formato armazenado: <c>PBKDF2-SHA256${iterações}${salt Base64}${hash Base64}</c>.
/// Como os parâmetros ficam gravados junto ao hash, é possível aumentar as iterações no futuro
/// sem invalidar senhas antigas (veja <see cref="NeedsRehash"/>).</para>
/// <para>Proteções: letras acentuadas são normalizadas para a forma composta (NIST SP 800-63B), para que "é"
/// digitado em teclados/sistemas diferentes gere o mesmo hash. A normalização é própria e determinística:
/// <see cref="string.Normalize(NormalizationForm)"/> não é usado porque não faz nada em ambientes com
/// <c>InvariantGlobalization</c>, o que faria o mesmo hash não validar em servidores diferentes.
/// O tamanho da senha é limitado e os parâmetros lidos do hash armazenado são validados, impedindo que
/// um registro adulterado cause consumo excessivo de CPU.</para>
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>Iterações padrão, conforme recomendação OWASP para PBKDF2-SHA256.</summary>
    public const int DefaultIterations = 600_000;

    /// <summary>Tamanho máximo de senha aceito (caracteres).</summary>
    public const int MaxPasswordLength = 1024;

    private const int MinIterations = 100_000;
    private const int MaxIterations = 10_000_000;
    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MinStoredBytes = 16;
    private const int MaxStoredBytes = 64;
    private const char Separator = '$';
    private static readonly byte[] DummySalt = new byte[SaltSize];
    private readonly int _iterations;

    /// <summary>Cria o hasher com a quantidade de iterações informada.</summary>
    public Pbkdf2PasswordHasher(int iterations = DefaultIterations)
    {
        _iterations = Guard.InRange(iterations, MinIterations, MaxIterations);
    }

    /// <inheritdoc />
    public string Hash(string password)
    {
        Guard.NotNull(password);
        if (password.Length == 0)
            throw new ArgumentException("A senha não pode ser vazia.", nameof(password));
        if (password.Length > MaxPasswordLength)
            throw new ArgumentException($"A senha deve ter no máximo {MaxPasswordLength} caracteres.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, _iterations, HashSize);
        return string.Join(Separator, Prefix, _iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Se <paramref name="hashedPassword"/> for <c>null</c> (usuário inexistente) ou inválido, executa uma derivação fictícia
    /// com as iterações configuradas e retorna <c>false</c>: o tempo de resposta fica equivalente ao de um usuário existente,
    /// sem revelar quais logins estão cadastrados.
    /// </remarks>
    public bool Verify(string password, string? hashedPassword)
    {
        Guard.NotNull(password);
        if (password.Length is 0 or > MaxPasswordLength)
            return false;

        if (!TryParse(hashedPassword, out int iterations, out var salt, out var expectedHash))
        {
            // Mesmo custo de uma verificação real (proteção contra enumeração de usuários por tempo de resposta)
            _ = Derive(password, DummySalt, _iterations, HashSize);
            return false;
        }

        var actualHash = Derive(password, salt, iterations, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <inheritdoc />
    public bool NeedsRehash(string hashedPassword) =>
        !TryParse(hashedPassword, out int iterations, out _, out var hash) || iterations < _iterations || hash.Length < HashSize;

    private static byte[] Derive(string password, byte[] salt, int iterations, int length)
    {
        // Normalização determinística (não depende de ICU): o mesmo hash é gerado em qualquer servidor/container
        var bytes = Encoding.UTF8.GetBytes(LatinDiacritics.Compose(password));
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(bytes, salt, iterations, HashAlgorithmName.SHA256, length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    // Valida rigorosamente o hash armazenado: ele também é tratado como entrada não confiável
    private static bool TryParse(string? hashedPassword, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = hash = [];
        if (string.IsNullOrWhiteSpace(hashedPassword) || hashedPassword.Length > 256)
            return false;

        var parts = hashedPassword.Split(Separator);
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations is < MinIterations or > MaxIterations)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length is >= MinStoredBytes and <= MaxStoredBytes
            && hash.Length is >= MinStoredBytes and <= MaxStoredBytes;
    }
}
