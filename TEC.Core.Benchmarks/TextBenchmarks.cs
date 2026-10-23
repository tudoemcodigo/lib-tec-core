using BenchmarkDotNet.Attributes;
using TEC.Core.Common.Serialization;
using TEC.Core.Numbers.Extensions;
using TEC.Core.Numbers.Words;
using TEC.Core.Responses;
using TEC.Core.Text.Extensions;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.Benchmarks;

/// <summary>Validação, formatação e mascaramento de documentos e funções de texto.</summary>
[MemoryDiagnoser]
public class TextBenchmarks
{
    private const string Cpf = "529.982.247-25";
    private const string Cnpj = "12.ABC.345/01DE-35";
    private const string Email = "fulano.de.tal@exemplo.com.br";
    private const string Sentence = "Ação Rápida: Configuração de Índices — Versão 2.0 (São Paulo)";

    [Benchmark]
    public bool ValidateCpf() => DocumentValidator.IsValidCpf(Cpf);

    [Benchmark]
    public bool ValidateAlphanumericCnpj() => DocumentValidator.IsValidCnpj(Cnpj);

    [Benchmark]
    public bool ValidateEmail() => DocumentValidator.IsValidEmail(Email);

    [Benchmark]
    public string FormatCpf() => DocumentFormatter.FormatCpf("52998224725");

    [Benchmark]
    public string MaskEmail() => SensitiveDataMasker.MaskEmail(Email);

    [Benchmark]
    public string RemoveAccents() => Sentence.RemoveAccents();

    [Benchmark]
    public string ToSlug() => Sentence.ToSlug();

    [Benchmark]
    public string CurrencyWords() => NumberToWordsConverter.ToCurrencyWords(1_234_567.89m);

    [Benchmark]
    public bool ParseBrazilianDecimal() => "R$ 1.234.567,89".TryParseBrazilianDecimal(out _);
}

/// <summary>Serialização do envelope padrão de resposta com as opções do TEC.Core.</summary>
[MemoryDiagnoser]
public class JsonBenchmarks
{
    public sealed record Customer(int Id, string Name, DateOnly BirthDate, decimal Balance);

    private readonly ApiResponse<Customer[]> _response = ApiResponse<Customer[]>.Ok(
        [.. Enumerable.Range(1, 50).Select(i => new Customer(i, $"Cliente {i}", new DateOnly(1990, 1, 1).AddDays(i), i * 10.5m))]);

    private string _json = string.Empty;

    [GlobalSetup]
    public void Setup() => _json = _response.ToJson();

    [Benchmark]
    public string Serialize() => _response.ToJson();

    [Benchmark]
    public ApiResponse<Customer[]>? Deserialize() => _json.FromJson<ApiResponse<Customer[]>>();
}
