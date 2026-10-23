using BenchmarkDotNet.Attributes;
using TEC.Core.Csv;

namespace TEC.Core.Benchmarks;

/// <summary>Escrita e leitura de CSV em memória (sem disco), com e sem a proteção contra fórmulas.</summary>
[MemoryDiagnoser]
public class CsvBenchmarks
{
    public sealed class Order
    {
        public int Id { get; set; }
        public string Customer { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public decimal Value { get; set; }
        public bool Paid { get; set; }
        public string? Notes { get; set; }
    }

    private Order[] _orders = [];
    private byte[] _csv = [];
    private CsvWriter _writer = null!;
    private CsvReader _reader = null!;

    [Params(1_000, 100_000)]
    public int Rows { get; set; }

    [Params(true, false)]
    public bool SanitizeFormulas { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var options = new CsvOptions { SanitizeFormulas = SanitizeFormulas };
        _writer = new CsvWriter(options);
        _reader = new CsvReader(options);
        _orders = [.. Enumerable.Range(1, Rows).Select(i => new Order
        {
            Id = i,
            Customer = i % 50 == 0 ? $"=Cliente \"{i}\"" : $"Cliente {i}",
            Date = new DateOnly(2026, 1, 1).AddDays(i % 365),
            Value = i * 1.25m,
            Paid = i % 2 == 0,
            Notes = i % 10 == 0 ? "Texto; com separador" : null
        })];

        using var stream = new MemoryStream();
        _writer.WriteAsync(stream, _orders).GetAwaiter().GetResult();
        _csv = stream.ToArray();
    }

    [Benchmark]
    public async Task<long> Write()
    {
        using var stream = new MemoryStream(_csv.Length);
        await _writer.WriteAsync(stream, _orders);
        return stream.Length;
    }

    [Benchmark]
    public async Task<int> Read()
    {
        int count = 0;
        await foreach (var _ in _reader.ReadAsync<Order>(new MemoryStream(_csv, writable: false)))
            count++;
        return count;
    }
}
