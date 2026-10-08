using System.Text;
using Microsoft.AspNetCore.Http.Features;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Abstractions;
using TEC.Core.Csv;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Csv.Attributes;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.DependencyInjection;
using TEC.Core.Exceptions;
using TEC.Core.Responses;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.SampleApi;

/// <summary>
/// API de exemplo com os usos típicos do TEC.Core. Alvo do gerador de carga e dos testes de carga.
/// </summary>
/// <remarks>
/// Configuração (appsettings, variáveis de ambiente ou linha de comando):
/// <c>Exemplo:MunicipiosPorUf</c> (padrão 200) e <c>Exemplo:IteracoesSenha</c> (padrão 100 000, o mínimo aceito).
/// A chave AES é gerada na inicialização apenas para o exemplo; em produção ela vem de um cofre de segredos.
/// </remarks>
public static class SampleApiApp
{
    /// <summary>Máximo de linhas exportadas por requisição.</summary>
    public const int MaxExportRows = 10_000;

    /// <summary>Tamanho máximo do CSV importado (1 MB).</summary>
    public const int MaxImportBytes = 1024 * 1024;

    /// <summary>Tamanho máximo do texto cifrado por requisição.</summary>
    public const int MaxTextLength = 64 * 1024;

    /// <summary>Senha cadastrada no exemplo (cenário de login).</summary>
    public const string SamplePassword = "S3nha-de-Exemplo!";

    /// <summary>Ano dos feriados sintéticos.</summary>
    public const int SampleYear = 2026;

    /// <summary>Cria a aplicação já configurada (sem iniciá-la).</summary>
    /// <param name="args">Argumentos da linha de comando.</param>
    /// <param name="configure">Ajustes extras no builder (ex.: porta em testes).</param>
    public static async Task<WebApplication> CreateAsync(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        int municipalities = builder.Configuration.GetValue("Exemplo:MunicipiosPorUf", 200);
        int iterations = builder.Configuration.GetValue("Exemplo:IteracoesSenha", 100_000);

        var calendar = await HolidayCalendar.CreateBuilder()
            .AddBrazilianNational()
            .AddHolidays(SampleData.Holidays(municipalities, SampleYear), "Exemplo")
            .BuildAsync();

        builder.Services.AddTecCore(options => options.PasswordHashIterations = iterations);
        builder.Services.AddBusinessDayCalculator(calendar);
        builder.Services.AddSingleton(sp => new SampleSecrets(
            sp.GetRequiredService<ISymmetricCryptography>().GenerateKey(),
            sp.GetRequiredService<IPasswordHasher>().Hash(SamplePassword)));

        var app = builder.Build();
        app.Use(HandleErrorsAsync);
        MapEndpoints(app);
        return app;
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/saude", () => Json(ApiResponse.Ok("ok")));

        app.MapGet("/feriados/{ibge:int}/{year:int}", (int ibge, int year, HolidayCalendar calendar) =>
            Json(ApiResponse<IReadOnlyList<Holiday>>.Ok(calendar.GetProvider(HolidayLocation.FromIbgeCode(ibge)).GetHolidays(year))));

        app.MapGet("/dias-uteis/vencimento", (int ibge, DateOnly date, int term, IBusinessDayCalculatorFactory factory) =>
        {
            var calculator = factory.For(HolidayLocation.FromIbgeCode(ibge));
            return Json(ApiResponse<DueDateResponse>.Ok(new DueDateResponse(
                calculator.AddBusinessDays(date, term), calculator.CountBusinessDaysInMonth(date.Year, date.Month))));
        });

        app.MapPost("/documentos/validar", (DocumentRequest request) =>
        {
            var document = request.Document ?? throw new RequestValidationException("document", "Documento é obrigatório.");
            bool isValid = DocumentValidator.IsValidCpfOrCnpj(document);
            return Json(ApiResponse<DocumentResponse>.Ok(new DocumentResponse(
                isValid,
                isValid ? DocumentFormatter.FormatCpfOrCnpj(document) : null,
                SensitiveDataMasker.Mask(document, 3, 2))));
        });

        app.MapPost("/criptografia/protecao", (TextRequest request, ISymmetricCryptography aes, SampleSecrets secrets) =>
        {
            var text = request.Text ?? throw new RequestValidationException("text", "Texto é obrigatório.");
            if (text.Length > MaxTextLength)
                throw new RequestValidationException("text", $"O texto deve ter no máximo {MaxTextLength} caracteres.");

            var encrypted = aes.Encrypt(Encoding.UTF8.GetBytes(text), secrets.AesKey);
            var decrypted = Encoding.UTF8.GetString(aes.Decrypt(encrypted, secrets.AesKey));
            return Json(ApiResponse<ProtectionResponse>.Ok(new ProtectionResponse(encrypted.Length, decrypted == text)));
        });

        app.MapPost("/senhas/verificar", (PasswordRequest request, IPasswordHasher hasher, SampleSecrets secrets) =>
        {
            var password = request.Password ?? throw new RequestValidationException("password", "Senha é obrigatória.");
            return hasher.Verify(password, secrets.PasswordHash)
                ? Json(ApiResponse.Ok("Senha correta."))
                : Json(ApiResponse.Unauthorized());
        });

        app.MapPost("/csv/importar", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            // Limite do corpo aplicado antes da leitura: o Kestrel recusa o excedente sem carregá-lo
            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                limit.MaxRequestBodySize = MaxImportBytes;

            int invalidRows = 0;
            var reader = new CsvReader(new CsvOptions { SkipInvalidRows = true, OnInvalidRow = _ => invalidRows++, MaxColumns = 32 });
            int rows = 0;
            decimal total = 0;
            await foreach (var order in reader.ReadAsync<OrderCsvRow>(context.Request.Body, cancellationToken))
            {
                rows++;
                total += order.Amount;
            }

            return Json(ApiResponse<ImportResponse>.Ok(new ImportResponse(rows, invalidRows, total)));
        });

        app.MapGet("/csv/exportar", async (HttpContext context, int rows, ICsvWriter writer, CancellationToken cancellationToken) =>
        {
            if (rows is < 1 or > MaxExportRows)
                throw new RequestValidationException("rows", $"Informe de 1 a {MaxExportRows} linhas.");

            context.Response.ContentType = "text/csv; charset=utf-8";
            await writer.WriteAsync(context.Response.Body, OrderCsvRow.Generate(rows), cancellationToken);
        });
    }

    // Erros padronizados: exceções da aplicação viram o ApiResponse correspondente; entrada inválida vira 400 genérico;
    // o restante vira 500 sem detalhes (ApiResponse.FromException nunca expõe a mensagem interna)
    private static async Task HandleErrorsAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && ex is not OperationCanceledException)
        {
            var response = ex switch
            {
                AppException => ApiResponse.FromException(ex, context.TraceIdentifier),
                // Status HTTP e envelope coerentes: o corpo também informa 413
                BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                    ApiResponse.Fail(StatusCodes.Status413PayloadTooLarge, "Arquivo maior que o limite de 1 MB.")
                        with { TraceId = context.TraceIdentifier },
                ArgumentException or FormatException or CsvException or BadHttpRequestException =>
                    ApiResponse.BadRequest("Requisição inválida.") with { TraceId = context.TraceIdentifier },
                _ => ApiResponse.FromException(ex, context.TraceIdentifier)
            };

            context.Response.StatusCode = response.StatusCode;
            await context.Response.WriteAsJsonAsync(response, JsonDefaults.Options);
        }
    }

    private static IResult Json(ApiResponse response) => Results.Json(response, JsonDefaults.Options, statusCode: response.StatusCode);
}

/// <summary>Segredos do exemplo (em produção, vindos de um cofre).</summary>
public sealed record SampleSecrets(byte[] AesKey, string PasswordHash);

/// <summary>Linha do CSV de pedidos (cabeçalho em português: Numero;Cliente;Data;Valor).</summary>
public sealed class OrderCsvRow
{
    /// <summary>Número do pedido.</summary>
    [CsvColumn("Numero", Order = 1)]
    public int Number { get; set; }

    /// <summary>Nome do cliente.</summary>
    [CsvColumn("Cliente", Order = 2)]
    public string Customer { get; set; } = string.Empty;

    /// <summary>Data do pedido.</summary>
    [CsvColumn("Data", Order = 3)]
    public DateOnly Date { get; set; }

    /// <summary>Valor do pedido.</summary>
    [CsvColumn("Valor", Order = 4)]
    public decimal Amount { get; set; }

    /// <summary>Pedidos sintéticos, gerados sob demanda (streaming).</summary>
    public static IEnumerable<OrderCsvRow> Generate(int count)
    {
        for (int i = 1; i <= count; i++)
            yield return new OrderCsvRow { Number = i, Customer = $"Cliente {i}", Date = new DateOnly(2026, 1, 1).AddDays(i % 365), Amount = i * 1.25m };
    }
}

/// <summary>Corpo de <c>POST /documentos/validar</c>.</summary>
public sealed record DocumentRequest(string? Document);

/// <summary>Resultado da validação de documento.</summary>
public sealed record DocumentResponse(bool IsValid, string? Formatted, string Masked);

/// <summary>Corpo de <c>POST /criptografia/protecao</c>.</summary>
public sealed record TextRequest(string? Text);

/// <summary>Resultado da proteção: tamanho cifrado e se a ida e volta conferiu.</summary>
public sealed record ProtectionResponse(int EncryptedLength, bool RoundTripMatches);

/// <summary>Corpo de <c>POST /senhas/verificar</c>.</summary>
public sealed record PasswordRequest(string? Password);

/// <summary>Vencimento calculado e dias úteis do mês.</summary>
public sealed record DueDateResponse(DateOnly DueDate, int BusinessDaysInMonth);

/// <summary>Resultado da importação de CSV.</summary>
public sealed record ImportResponse(int Rows, int InvalidRows, decimal Total);
