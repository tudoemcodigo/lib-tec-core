using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Limite de requisições/tentativas excedido (HTTP 429). Ex.: muitas tentativas de login ou de envio de código.
/// </summary>
public class RateLimitExceededException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "LIMITE_EXCEDIDO";

    /// <summary>Mensagem padrão.</summary>
    public const string DefaultMessage = "Muitas requisições. Aguarde e tente novamente.";

    /// <summary>Cria a exceção.</summary>
    /// <param name="retryAfter">Tempo sugerido de espera (opcional, até 24 horas), para o cabeçalho HTTP <c>Retry-After</c>.</param>
    /// <param name="message">Mensagem específica (opcional).</param>
    public RateLimitExceededException(TimeSpan? retryAfter = null, string message = DefaultMessage)
        : base(DefaultCode, message, ErrorType.TooManyRequests)
    {
        if (retryAfter is { } value && (value <= TimeSpan.Zero || value > TimeSpan.FromHours(24)))
            throw new ArgumentOutOfRangeException(nameof(retryAfter), "O tempo de espera deve estar entre 0 e 24 horas.");

        RetryAfter = retryAfter;
    }

    /// <summary>Tempo sugerido de espera antes de tentar novamente.</summary>
    public TimeSpan? RetryAfter { get; }
}
