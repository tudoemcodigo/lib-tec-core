using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Falha em serviço externo ou integração (HTTP 502). Ex.: API de terceiros fora do ar, timeout, resposta inválida.
/// </summary>
/// <remarks>
/// A mensagem e o nome do serviço são usados apenas em logs: o cliente recebe sempre uma mensagem genérica,
/// pois detalhes de integração (URLs, endpoints, respostas) revelam a infraestrutura.
/// </remarks>
/// <example>
/// <code>
/// catch (HttpRequestException ex)
/// {
///     throw new IntegrationException("ReceitaFederal", "Falha ao consultar situação cadastral.", ex);
/// }
/// </code>
/// </example>
public class IntegrationException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "FALHA_INTEGRACAO";

    /// <summary>Cria a exceção.</summary>
    /// <param name="serviceName">Nome do serviço externo (ex.: "ReceitaFederal"). Usado apenas em logs.</param>
    /// <param name="message">Descrição da falha. Usada apenas em logs.</param>
    /// <param name="innerException">Exceção original.</param>
    public IntegrationException(string serviceName, string message, Exception? innerException = null)
        : base(DefaultCode, message, ErrorType.ExternalService, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ServiceName = serviceName;
    }

    /// <summary>Nome do serviço externo que falhou.</summary>
    public string ServiceName { get; }
}
