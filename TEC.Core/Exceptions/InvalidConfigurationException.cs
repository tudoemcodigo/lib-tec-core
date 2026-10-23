using TEC.Core.Common.Results;

namespace TEC.Core.Exceptions;

/// <summary>
/// Configuração ausente ou inválida (HTTP 500). Ex.: chave de criptografia não configurada, connection string vazia.
/// </summary>
/// <remarks>
/// Nome diferente de <c>System.Configuration.ConfigurationException</c> para evitar conflito.
/// A mensagem nunca é exposta ao cliente. Informe apenas o NOME da configuração, jamais o valor.
/// </remarks>
/// <example>
/// <code>
/// var key = config["Crypto:Key"] ?? throw new InvalidConfigurationException("Crypto:Key");
/// </code>
/// </example>
public class InvalidConfigurationException : AppException
{
    /// <summary>Código padrão.</summary>
    public const string DefaultCode = "CONFIGURACAO_INVALIDA";

    /// <summary>Cria a exceção para uma configuração ausente ou inválida.</summary>
    /// <param name="settingName">Nome da configuração (ex.: "ConnectionStrings:Default"). Nunca o valor.</param>
    /// <param name="innerException">Exceção original.</param>
    public InvalidConfigurationException(string settingName, Exception? innerException = null)
        : base(DefaultCode, BuildMessage(settingName), ErrorType.Failure, innerException)
    {
        SettingName = settingName;
    }

    /// <summary>Nome da configuração ausente ou inválida.</summary>
    public string SettingName { get; }

    private static string BuildMessage(string settingName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingName);
        return $"A configuração '{settingName}' está ausente ou é inválida.";
    }
}
