using System.Runtime.CompilerServices;

namespace TEC.Core.Common.Guards;

/// <summary>
/// Validações de argumentos padronizadas. O nome do parâmetro é preenchido automaticamente.
/// </summary>
public static class Guard
{
    /// <summary>Garante que o valor não seja nulo.</summary>
    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    /// <summary>Garante que a string não seja nula, vazia ou composta apenas por espaços.</summary>
    public static string NotNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    /// <summary>Garante que a coleção não seja nula nem vazia.</summary>
    public static IReadOnlyCollection<T> NotEmpty<T>(IReadOnlyCollection<T>? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        if (value.Count == 0)
            throw new ArgumentException("A coleção não pode ser vazia.", paramName);
        return value;
    }

    /// <summary>Garante que o valor esteja dentro do intervalo informado (inclusive).</summary>
    /// <remarks>O valor recebido não entra na mensagem da exceção (pode ser dado pessoal ou vir de entrada externa).</remarks>
    public static T InRange<T>(T value, T min, T max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (min.CompareTo(max) > 0)
            throw new ArgumentException("O limite mínimo não pode ser maior que o máximo.", nameof(min));

        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(paramName, $"O valor deve estar entre {min} e {max}.");
        return value;
    }

    /// <summary>Garante que o valor seja maior que zero.</summary>
    public static int Positive(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
        return value;
    }

    /// <summary>Lança <see cref="ArgumentException"/> caso a condição seja verdadeira.</summary>
    /// <param name="condition">Condição que torna o argumento inválido.</param>
    /// <param name="message">Mensagem da exceção.</param>
    /// <param name="paramName">
    /// Nome do parâmetro inválido (use <c>nameof</c>). Não é preenchido automaticamente: a expressão da condição
    /// (ex.: <c>"items.Count == 0"</c>) não é um nome de parâmetro.
    /// </param>
    public static void Against(bool condition, string message, string? paramName = null)
    {
        if (condition)
            throw new ArgumentException(message, paramName);
    }
}
