using System.Runtime.CompilerServices;
using System.Text.Json;
using TEC.Core.Common.Guards;
using TEC.Core.Dates.Holidays.Internal;

namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Feriados em JSON (arquivo ou stream): um array de objetos. Compatível com Native AOT (sem serialização por reflexão).
/// </summary>
/// <remarks>
/// Formato padrão (nomes sem diferenciar maiúsculas e acentos; <c>uf</c> e <c>codigoIbge</c> são opcionais):
/// <code>
/// [
///   { "data": "2026-01-25", "descricao": "Aniversário de São Paulo", "uf": "SP", "codigoIbge": 3550308 },
///   { "data": "09/07/2026", "descricao": "Revolução Constitucionalista", "uf": "SP" }
/// ]
/// </code>
/// Também aceita <c>date</c>/<c>name</c>/<c>description</c>/<c>ibgeCode</c> (formato da BrasilAPI). Para outros
/// formatos, informe um <c>mapper</c>; ele pode chamar <see cref="MapDefault"/> e devolver <c>null</c> para ignorar o item.
/// </remarks>
public sealed class JsonHolidaySource : IHolidaySource
{
    /// <summary>Tamanho máximo do JSON (64 MB). Evita esgotar a memória com arquivos ou respostas inesperadas.</summary>
    public const int MaxJsonBytes = 64 * 1024 * 1024;

    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 16 };

    private readonly string? _filePath;
    private readonly Func<Stream>? _openStream;
    private readonly Func<JsonElement, Holiday?> _mapper;

    /// <summary>Lê de um arquivo. O caminho não deve vir do usuário final (risco de <i>path traversal</i>).</summary>
    public JsonHolidaySource(string filePath, Func<JsonElement, Holiday?>? mapper = null)
    {
        _filePath = Guard.NotNullOrWhiteSpace(filePath);
        _mapper = mapper ?? MapDefault;
        Name = filePath;
    }

    /// <summary>Lê de um stream aberto a cada carga (ex.: recurso embutido). O stream é descartado ao final da leitura.</summary>
    public JsonHolidaySource(Func<Stream> openStream, string name, Func<JsonElement, Holiday?>? mapper = null)
    {
        _openStream = Guard.NotNull(openStream);
        Name = Guard.NotNullOrWhiteSpace(name);
        _mapper = mapper ?? MapDefault;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public async IAsyncEnumerable<Holiday> LoadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Stream stream;
        if (_filePath is not null)
        {
            if (!File.Exists(_filePath))
                throw new FileNotFoundException("Arquivo de feriados não encontrado.", _filePath);

            stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        else
        {
            stream = _openStream!() ?? throw new InvalidOperationException("A função que abre o stream de feriados retornou null.");
        }

        List<Holiday> holidays;
        await using (stream.ConfigureAwait(false))
            holidays = await ReadAsync(stream, _mapper, cancellationToken).ConfigureAwait(false);

        foreach (var holiday in holidays)
            yield return holiday;
    }

    /// <summary>
    /// Converte um objeto no formato padrão (veja <see cref="JsonHolidaySource"/>). Campos desconhecidos são ignorados.
    /// </summary>
    /// <exception cref="FormatException">Item que não é objeto, sem data ou descrição, ou com valor em formato inválido.</exception>
    /// <exception cref="ArgumentException">UF ou código IBGE inválidos.</exception>
    public static Holiday? MapDefault(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException("Cada feriado deve ser um objeto JSON.");

        DateOnly? date = null;
        string? description = null;
        string? uf = null;
        int? ibgeCode = null;

        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value;
            switch (HolidayFields.Resolve(property.Name))
            {
                case HolidayField.Date:
                    date = HolidayFields.ParseDate(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
                    break;
                case HolidayField.Description:
                    description = GetOptionalString(value, "descricao");
                    break;
                case HolidayField.Uf:
                    uf = GetOptionalString(value, "uf");
                    break;
                case HolidayField.IbgeCode:
                    ibgeCode = value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.Number when value.TryGetInt32(out int code) => code,
                        JsonValueKind.String => HolidayFields.ParseIbgeCode(value.GetString()),
                        _ => throw new FormatException("Código IBGE inválido: informe somente os 7 dígitos."),
                    };
                    break;
            }
        }

        if (date is null)
            throw new FormatException("Campo 'data' ausente.");
        if (description is null)
            throw new FormatException("Campo 'descricao' ausente.");

        return new Holiday(date.Value, description) { Uf = uf, IbgeCode = ibgeCode };
    }

    /// <summary>Lê um array JSON de feriados do stream (até <see cref="MaxJsonBytes"/>). Usado também pela fonte HTTP.</summary>
    internal static async Task<List<Holiday>> ReadAsync(Stream stream, Func<JsonElement, Holiday?> mapper, CancellationToken cancellationToken)
    {
        using var buffer = await CopyLimitedAsync(stream, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(buffer, DocumentOptions, cancellationToken).ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new FormatException("O JSON de feriados deve ser um array de objetos.");

        var holidays = new List<Holiday>();
        int index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            try
            {
                if (mapper(element) is { } holiday)
                    holidays.Add(holiday);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
            {
                throw new FormatException($"Feriado na posição {index} do JSON: {ex.Message}", ex);
            }

            index++;
        }

        return holidays;
    }

    private static string? GetOptionalString(JsonElement value, string field) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        _ => throw new FormatException($"Campo '{field}' deve ser texto."),
    };

    private static async Task<MemoryStream> CopyLimitedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxJsonBytes)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
                throw new InvalidDataException($"O JSON de feriados passa do limite de {MaxJsonBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }
}
