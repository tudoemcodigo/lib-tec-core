using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TEC.Core.Common.Guards;

namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Feriados obtidos por HTTP GET de uma API que devolve um array JSON (formato padrão em <see cref="JsonHolidaySource"/>).
/// </summary>
/// <remarks>
/// O <see cref="HttpClient"/> é do chamador (use <c>IHttpClientFactory</c> e configure timeout/retry nele) e não é descartado.
/// O nome da fonte omite as credenciais (<c>usuario:senha@</c>), a query string e o fragmento da URL, que podem conter
/// chaves de acesso.
/// </remarks>
/// <example>
/// <code>
/// var calendar = await HolidayCalendar.CreateBuilder()
///     .AddSource(HttpHolidaySource.BrasilApi(httpClient, 2026), optional: true)
///     .BuildAsync();
/// </code>
/// </example>
public sealed class HttpHolidaySource : IHolidaySource
{
    /// <summary>Endereço da API de feriados nacionais da BrasilAPI (o ano vai no final).</summary>
    public const string BrasilApiBaseUrl = "https://brasilapi.com.br/api/feriados/v1/";

    private readonly HttpClient _httpClient;
    private readonly Uri _requestUri;
    private readonly Func<JsonElement, Holiday?> _mapper;

    /// <summary>Cria a fonte.</summary>
    /// <param name="httpClient">Cliente HTTP (não é descartado pela fonte).</param>
    /// <param name="requestUri">URL absoluta, ou relativa ao <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="mapper">Conversão de cada item do array (padrão: <see cref="JsonHolidaySource.MapDefault"/>).</param>
    public HttpHolidaySource(HttpClient httpClient, Uri requestUri, Func<JsonElement, Holiday?>? mapper = null)
    {
        _httpClient = Guard.NotNull(httpClient);
        _requestUri = Guard.NotNull(requestUri);
        _mapper = mapper ?? JsonHolidaySource.MapDefault;
        Name = SafeName(requestUri);
    }

    // Nome sem credenciais (user:senha@), query string e fragmento: aparece em HolidaySourceFailure, exceções e logs
    private static string SafeName(Uri uri)
    {
        if (uri.IsAbsoluteUri)
            return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);

        var path = uri.OriginalString.Split('?', '#')[0];
        // Relativa ao esquema ("//user:senha@host/caminho"): a autoridade pode trazer credenciais
        if (path.StartsWith("//", StringComparison.Ordinal) && path.IndexOf('@', 2) is var at and >= 0)
        {
            int slash = path.IndexOf('/', 2);
            if (slash < 0 || at < slash)
                return "//" + path[(at + 1)..];
        }

        return path;
    }

    /// <summary>
    /// Feriados nacionais do ano na BrasilAPI (<c>brasilapi.com.br/api/feriados/v1/{ano}</c>), que atende de 1900 a 2199.
    /// </summary>
    public static HttpHolidaySource BrasilApi(HttpClient httpClient, int year)
    {
        Guard.InRange(year, 1900, 2199);
        return new HttpHolidaySource(httpClient, new Uri(BrasilApiBaseUrl + year.ToString(CultureInfo.InvariantCulture)));
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public async IAsyncEnumerable<Holiday> LoadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<Holiday> holidays;
        using (var response = await _httpClient.GetAsync(_requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
                holidays = await JsonHolidaySource.ReadAsync(stream, _mapper, cancellationToken).ConfigureAwait(false);
        }

        foreach (var holiday in holidays)
            yield return holiday;
    }
}
