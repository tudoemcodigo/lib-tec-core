using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using TEC.Core.SampleApi;

namespace TEC.Core.LoadTests.Infrastructure;

/// <summary>
/// API de exemplo hospedada no próprio processo, em Kestrel real (sockets TCP em 127.0.0.1, porta livre escolhida pelo SO).
/// </summary>
public sealed class SampleApiHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private SampleApiHost(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    /// <summary>Endereço da API (ex.: http://127.0.0.1:53817/).</summary>
    public Uri BaseAddress { get; }

    /// <summary>Inicia a API.</summary>
    /// <param name="passwordIterations">Iterações do PBKDF2 do cenário de senha (mínimo 100 000).</param>
    public static async Task<SampleApiHost> StartAsync(int passwordIterations = 100_000)
    {
        var app = await SampleApiApp.CreateAsync([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration["Exemplo:IteracoesSenha"] = passwordIterations.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });

        await app.StartAsync();
        return new SampleApiHost(app, new Uri(app.Urls.First()));
    }

    /// <summary>Cliente HTTP com uma conexão por worker.</summary>
    public HttpClient CreateClient(int concurrency) =>
        new(new SocketsHttpHandler { MaxConnectionsPerServer = concurrency }, disposeHandler: true)
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(30)
        };

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
