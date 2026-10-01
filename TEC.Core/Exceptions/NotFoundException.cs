using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Recurso não encontrado (HTTP 404).
/// </summary>
/// <remarks>
/// A mensagem não inclui o identificador pesquisado de propósito: ele pode ser um dado pessoal (ex.: CPF)
/// e permitiria enumerar registros existentes.
/// </remarks>
/// <example>
/// <code>
/// var cliente = await repo.ObterAsync(id) ?? throw NotFoundException.For("Cliente");
/// </code>
/// </example>
public class NotFoundException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "NAO_ENCONTRADO";

    /// <summary>Cria a exceção com o código padrão.</summary>
    public NotFoundException(string message)
        : base(DefaultCode, message, ErrorType.NotFound)
    {
    }

    /// <summary>Cria a exceção com código específico.</summary>
    public NotFoundException(string code, string message, Exception? innerException = null)
        : base(code, message, ErrorType.NotFound, innerException)
    {
    }

    /// <summary>Cria a exceção para um tipo de recurso: "Cliente não encontrado(a).".</summary>
    /// <param name="resourceName">Nome do recurso (ex.: "Cliente", "Pedido").</param>
    public static NotFoundException For(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return new NotFoundException(DefaultCode, $"{resourceName.Trim()} não encontrado(a).");
    }
}
