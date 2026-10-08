using System.Globalization;
using System.Text;
using TEC.Core.Common.Guards;

namespace TEC.Core.Csv;

/// <summary>
/// Configurações de leitura e escrita de CSV. Os valores padrão são compatíveis com o Excel em pt-BR.
/// Cada propriedade é validada na atribuição: configurações inválidas falham imediatamente.
/// </summary>
public sealed class CsvOptions
{
    /// <summary>Instância com as configurações padrão.</summary>
    public static CsvOptions Default { get; } = new();

    /// <summary>
    /// Separador de colunas. Padrão: <c>;</c>.
    /// Não pode ser aspas, quebra de linha, caractere de controle (exceto TAB), letra ou dígito.
    /// </summary>
    public char Delimiter
    {
        get;
        init
        {
            if (value is '"' or '\r' or '\n' || (char.IsControl(value) && value != '\t') || char.IsLetterOrDigit(value) || char.IsSurrogate(value))
                throw new ArgumentException("Separador inválido. Use, por exemplo, ';', ',', '|' ou TAB.", nameof(Delimiter));
            field = value;
        }
    } = ';';

    /// <summary>Codificação. Padrão: UTF-8 com BOM (o Excel reconhece acentos corretamente).</summary>
    public Encoding Encoding
    {
        get;
        init => field = Guard.NotNull(value, nameof(Encoding));
    } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Indica se a primeira linha é o cabeçalho. Padrão: <c>true</c>.</summary>
    public bool HasHeader { get; init; } = true;

    /// <summary>Cultura usada para números e datas. Padrão: pt-BR.</summary>
    public CultureInfo Culture
    {
        get;
        init => field = Guard.NotNull(value, nameof(Culture));
    } = Common.Globalization.BrazilianCulture.Instance;

    /// <summary>
    /// Remove espaços nas pontas dos valores de texto na leitura. Padrão: <c>true</c>.
    /// Campos entre aspas nunca são aparados (as aspas indicam que os espaços fazem parte do valor).
    /// </summary>
    public bool TrimValues { get; init; } = true;

    /// <summary>Ignora linhas vazias na leitura. Padrão: <c>true</c>.</summary>
    public bool SkipEmptyLines { get; init; } = true;

    /// <summary>
    /// Ignora linhas com valores inválidos em vez de lançar <see cref="CsvException"/>. Padrão: <c>false</c>.
    /// Use <see cref="OnInvalidRow"/> para registrar as linhas descartadas.
    /// </summary>
    public bool SkipInvalidRows { get; init; }

    /// <summary>Callback chamado para cada linha inválida ignorada (quando <see cref="SkipInvalidRows"/> é <c>true</c>).</summary>
    public Action<CsvException>? OnInvalidRow { get; init; }

    /// <summary>Quebra de linha na escrita: <c>\r\n</c> (padrão, RFC 4180), <c>\n</c> ou <c>\r</c>.</summary>
    public string NewLine
    {
        get;
        init => field = value is "\r\n" or "\n" or "\r"
            ? value
            : throw new ArgumentException("Quebra de linha inválida. Use \"\\r\\n\", \"\\n\" ou \"\\r\".", nameof(NewLine));
    } = "\r\n";

    /// <summary>Envolve todos os campos em aspas na escrita. Padrão: <c>false</c> (somente quando necessário).</summary>
    public bool QuoteAllFields { get; init; }

    /// <summary>Forma de escrita de enums. Padrão: nome do membro.</summary>
    public CsvEnumFormat EnumFormat
    {
        get;
        init => field = Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(EnumFormat), "Formato de enum inválido.");
    } = CsvEnumFormat.Name;

    /// <summary>
    /// Protege contra injeção de fórmulas (OWASP CSV Injection): na escrita, textos iniciados por
    /// <c>= + - @</c>, TAB ou CR recebem um apóstrofo no início; na leitura, esse apóstrofo é removido.
    /// Aplica-se somente a propriedades do tipo texto (números negativos não são afetados). Padrão: <c>true</c>.
    /// </summary>
    public bool SanitizeFormulas { get; init; } = true;

    /// <summary>
    /// Tamanho máximo de um campo na leitura (1 a 100.000.000 caracteres). Protege contra arquivos maliciosos
    /// que esgotariam a memória (ex.: aspas nunca fechadas). Padrão: 1.048.576.
    /// </summary>
    public int MaxFieldLength
    {
        get;
        init => field = Guard.InRange(value, 1, 100_000_000, nameof(MaxFieldLength));
    } = 1024 * 1024;

    /// <summary>Quantidade máxima de colunas por linha na leitura (1 a 100.000). Padrão: 1.024.</summary>
    public int MaxColumns
    {
        get;
        init => field = Guard.InRange(value, 1, 100_000, nameof(MaxColumns));
    } = 1024;

    /// <summary>
    /// Tamanho máximo de um registro (linha lógica, somando todos os campos e separadores) na leitura,
    /// de 1 a 1.000.000.000 caracteres. Complementa <see cref="MaxFieldLength"/> e <see cref="MaxColumns"/>:
    /// sem ele, um único registro poderia acumular <c>MaxColumns × MaxFieldLength</c> caracteres em memória.
    /// Padrão: 4.194.304 (4 Mi caracteres).
    /// </summary>
    public int MaxRecordLength
    {
        get;
        init => field = Guard.InRange(value, 1, 1_000_000_000, nameof(MaxRecordLength));
    } = 4 * 1024 * 1024;

    /// <summary>
    /// Exige que toda linha de dados tenha exatamente a mesma quantidade de colunas do cabeçalho
    /// (ou, sem cabeçalho, das propriedades mapeadas). Padrão: <c>false</c>: linhas curtas deixam as
    /// propriedades sem coluna com o valor padrão, e colunas extras são ignoradas.
    /// A violação gera <see cref="CsvException"/> (ou descarta a linha, com <see cref="SkipInvalidRows"/>).
    /// </summary>
    public bool StrictColumnCount { get; init; }

    /// <summary>
    /// Exige que o cabeçalho contenha uma coluna para cada propriedade gravável do tipo de destino
    /// (exceto as marcadas com <c>[CsvIgnore]</c>). Padrão: <c>false</c>: basta uma coluna corresponder.
    /// A violação gera <see cref="CsvException"/> com o nome das colunas ausentes. Só se aplica com <see cref="HasHeader"/>.
    /// </summary>
    public bool RequireAllColumns { get; init; }

    /// <summary>
    /// Aceita separador de milhar na leitura de <c>decimal</c>, <c>double</c> e <c>float</c>. Padrão: <c>true</c>.
    /// Mesmo habilitado, o separador só é aceito entre grupos completos da cultura (em pt-BR, "1.234,56");
    /// valores como "1.5" ou "1.2.3" geram erro de conversão em vez de serem lidos como 15 ou 123.
    /// Desabilite para recusar qualquer separador de milhar (ex.: arquivos gerados por sistemas que nunca o usam).
    /// </summary>
    public bool AllowThousandsSeparator { get; init; } = true;

    /// <summary>
    /// Inclui o valor original e a exceção interna nos erros de conversão (<see cref="CsvException"/>).
    /// Padrão: <c>false</c>, para que dados pessoais do arquivo (CPF, e-mail etc.) não vazem para logs.
    /// </summary>
    public bool IncludeRawValueInErrors { get; init; }

    /// <summary>Tamanho do buffer de leitura/escrita em caracteres (1 KB a 16 MB). Padrão: 64 KB.</summary>
    public int BufferSize
    {
        get;
        init => field = Guard.InRange(value, 1024, 16 * 1024 * 1024, nameof(BufferSize));
    } = 64 * 1024;
}
