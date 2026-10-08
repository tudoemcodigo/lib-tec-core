using System.Text.Json.Serialization;
using TEC.Core.Common.Results;

namespace TEC.Core.Responses;

/// <summary>
/// Erro retornado na resposta da API.
/// </summary>
/// <param name="Code">Código do erro (ex.: "CPF_INVALIDO"). Obrigatório.</param>
/// <param name="Message">Mensagem descritiva. Obrigatória.</param>
/// <param name="Field">Campo relacionado (em erros de validação).</param>
public sealed record ApiError(string Code, string Message, string? Field = null)
{
    // Validação no inicializador (construtor) e no init (expressão "with")

    /// <summary>Código do erro.</summary>
    public string Code
    {
        get;
        init => field = RequireText(value, nameof(Code));
    } = RequireText(Code, nameof(Code));

    /// <summary>Mensagem descritiva.</summary>
    public string Message
    {
        get;
        init => field = RequireText(value, nameof(Message));
    } = RequireText(Message, nameof(Message));

    /// <summary>Campo relacionado (omitido no JSON quando nulo).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; } = Field;

    /// <summary>Converte um <see cref="Error"/> de domínio em erro de API.</summary>
    public static ApiError FromError(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApiError(error.Code, error.Message, error.Field);
    }

    private static string RequireText(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }
}
