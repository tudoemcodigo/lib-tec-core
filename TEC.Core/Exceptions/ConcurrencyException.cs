using System.Runtime.CompilerServices;

namespace TEC.Core.Exceptions;

/// <summary>
/// O registro foi alterado por outro usuário/processo desde que foi lido (HTTP 409).
/// Use com controle de concorrência otimista (versão/RowVersion).
/// </summary>
public class ConcurrencyException : ConflictException
{
    /// <summary>Código padrão.</summary>
    public new const string DefaultCode = "CONCORRENCIA";

    /// <summary>Mensagem padrão.</summary>
    public const string DefaultMessage = "O registro foi alterado por outro usuário. Recarregue os dados e tente novamente.";

    /// <summary>Cria a exceção com código e mensagem padrão.</summary>
    /// <remarks>
    /// Tem prioridade na resolução de sobrecarga: <c>new ConcurrencyException(null)</c> resolve para este construtor
    /// (mensagem padrão), em vez de ser ambíguo com <see cref="ConcurrencyException(string, Exception?)"/>.
    /// </remarks>
    [OverloadResolutionPriority(1)]
    public ConcurrencyException(Exception? innerException = null)
        : base(DefaultCode, DefaultMessage, innerException)
    {
    }

    /// <summary>Cria a exceção com mensagem específica.</summary>
    public ConcurrencyException(string message, Exception? innerException = null)
        : base(DefaultCode, message, innerException)
    {
    }
}
