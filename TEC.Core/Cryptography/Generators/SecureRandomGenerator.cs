using System.Security.Cryptography;
using TEC.Core.Common.Guards;
using TEC.Core.Polyfills;

namespace TEC.Core.Cryptography.Generators;

/// <summary>
/// Geração de valores aleatórios criptograficamente seguros (senhas, tokens, códigos OTP).
/// </summary>
public static class SecureRandomGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Special = "!@#$%&*()-_=+[]{};:,.?";
    private const string AllDigits = "0123456789";

    /// <summary>
    /// Gera uma senha aleatória contendo ao menos um caractere de cada grupo selecionado.
    /// Caracteres ambíguos (0, O, 1, l, I) são excluídos para facilitar a digitação.
    /// </summary>
    public static string GeneratePassword(int length = 16, bool includeUppercase = true, bool includeLowercase = true,
        bool includeDigits = true, bool includeSpecial = true)
    {
        var groups = new List<string>(4);
        if (includeUppercase) groups.Add(Uppercase);
        if (includeLowercase) groups.Add(Lowercase);
        if (includeDigits) groups.Add(Digits);
        if (includeSpecial) groups.Add(Special);

        Guard.Against(groups.Count == 0, "Selecione ao menos um grupo de caracteres.");
        Guard.InRange(length, groups.Count, 1024);

        var allChars = string.Concat(groups);
        var password = new char[length];

        // Garante ao menos um caractere de cada grupo e completa com caracteres de todos os grupos
        for (int i = 0; i < groups.Count; i++)
            password[i] = groups[i][RandomNumberGenerator.GetInt32(groups[i].Length)];

        for (int i = groups.Count; i < length; i++)
            password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];

        RandomNumberGenerator.Shuffle(password.AsSpan());
        return new string(password);
    }

    /// <summary>Gera um token aleatório em Base64Url (seguro para URLs), ex.: tokens de redefinição de senha.</summary>
    /// <param name="byteLength">Quantidade de bytes aleatórios (32 bytes = 256 bits).</param>
    public static string GenerateToken(int byteLength = 32)
    {
        Guard.InRange(byteLength, 16, 1024);
        return EncodingCompat.ToBase64Url(RandomNumberGenerator.GetBytes(byteLength));
    }

    /// <summary>Gera um código numérico (OTP), ex.: "048213". Preserva zeros à esquerda.</summary>
    public static string GenerateNumericCode(int digits = 6)
    {
        Guard.InRange(digits, 4, 12);
        return RandomNumberGenerator.GetString(AllDigits, digits);
    }

    /// <summary>Gera uma string hexadecimal aleatória.</summary>
    public static string GenerateHex(int byteLength = 16)
    {
        Guard.InRange(byteLength, 1, 1024);
        return EncodingCompat.ToHexLower(RandomNumberGenerator.GetBytes(byteLength));
    }
}
