namespace TEC.Core.Text.Internal;

/// <summary>
/// Cálculo de dígitos verificadores (módulo 11) de documentos brasileiros.
/// </summary>
internal static class CheckDigitCalculator
{
    private static readonly int[] CpfWeights1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] CpfWeights2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] CnpjWeights1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] CnpjWeights2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PisWeights = [3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Calcula os 2 dígitos verificadores do CPF a partir dos 9 primeiros dígitos.</summary>
    public static string CalculateCpf(ReadOnlySpan<char> base9)
    {
        int dv1 = Modulo11(base9, CpfWeights1);
        Span<char> base10 = stackalloc char[10];
        base9.CopyTo(base10);
        base10[9] = (char)('0' + dv1);
        int dv2 = Modulo11(base10, CpfWeights2);
        return $"{dv1}{dv2}";
    }

    /// <summary>
    /// Calcula os 2 dígitos verificadores do CNPJ a partir dos 12 primeiros caracteres.
    /// Suporta o CNPJ alfanumérico (IN RFB nº 2.229/2024): o valor de cada caractere é o código ASCII menos 48.
    /// </summary>
    public static string CalculateCnpj(ReadOnlySpan<char> base12)
    {
        int dv1 = Modulo11(base12, CnpjWeights1);
        Span<char> base13 = stackalloc char[13];
        base12.CopyTo(base13);
        base13[12] = (char)('0' + dv1);
        int dv2 = Modulo11(base13, CnpjWeights2);
        return $"{dv1}{dv2}";
    }

    /// <summary>Calcula o dígito verificador do PIS/PASEP/NIT a partir dos 10 primeiros dígitos.</summary>
    public static int CalculatePis(ReadOnlySpan<char> base10)
    {
        int sum = 0;
        for (int i = 0; i < PisWeights.Length; i++)
            sum += (base10[i] - '0') * PisWeights[i];

        int dv = 11 - (sum % 11);
        return dv >= 10 ? 0 : dv;
    }

    private static int Modulo11(ReadOnlySpan<char> value, int[] weights)
    {
        int sum = 0;
        for (int i = 0; i < weights.Length; i++)
            sum += (value[i] - '0') * weights[i];

        int remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
