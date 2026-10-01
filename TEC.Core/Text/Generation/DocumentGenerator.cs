using System.Security.Cryptography;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Internal;

namespace TEC.Core.Text.Generation;

/// <summary>
/// Geração de documentos válidos para testes e massa de dados.
/// </summary>
/// <remarks>Os documentos gerados são matematicamente válidos, mas fictícios. Não use em produção.</remarks>
public static class DocumentGenerator
{
    private const string AlphaNumericChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Gera um CPF válido.</summary>
    public static string GenerateCpf(bool formatted = false)
    {
        Span<char> base9 = stackalloc char[9];
        FillDigits(base9);
        var cpf = string.Concat(base9, CheckDigitCalculator.CalculateCpf(base9));
        return formatted ? DocumentFormatter.FormatCpf(cpf) : cpf;
    }

    /// <summary>Gera um CNPJ válido (matriz: ordem 0001).</summary>
    /// <param name="formatted">Retorna com máscara.</param>
    /// <param name="alphanumeric">Gera no novo formato alfanumérico.</param>
    public static string GenerateCnpj(bool formatted = false, bool alphanumeric = false)
    {
        Span<char> base12 = stackalloc char[12];
        if (alphanumeric)
        {
            for (int i = 0; i < 8; i++)
                base12[i] = AlphaNumericChars[RandomNumberGenerator.GetInt32(AlphaNumericChars.Length)];
        }
        else
        {
            FillDigits(base12[..8]);
        }

        "0001".AsSpan().CopyTo(base12[8..]);
        var cnpj = string.Concat(base12, CheckDigitCalculator.CalculateCnpj(base12));
        return formatted ? DocumentFormatter.FormatCnpj(cnpj) : cnpj;
    }

    /// <summary>Gera um PIS/PASEP/NIT válido.</summary>
    public static string GeneratePis(bool formatted = false)
    {
        Span<char> base10 = stackalloc char[10];
        FillDigits(base10);
        var pis = string.Concat(base10, CheckDigitCalculator.CalculatePis(base10).ToString());
        return formatted ? DocumentFormatter.FormatPis(pis) : pis;
    }

    private static void FillDigits(Span<char> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));

        // Evita sequências com todos os dígitos iguais (consideradas inválidas)
        if (buffer.IndexOfAnyExcept(buffer[0]) < 0)
            buffer[^1] = buffer[0] == '9' ? '0' : (char)(buffer[0] + 1);
    }
}
