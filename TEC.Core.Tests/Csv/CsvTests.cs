using System.ComponentModel;
using System.Text;
using TEC.Core.Csv;
using TEC.Core.Csv.Attributes;
using TUnit.Assertions.Enums;

namespace TEC.Core.Tests.Csv;

public class CsvTests
{
    public enum CustomerStatus
    {
        [Description("Cliente ativo")] Active = 1,
        [Description("Cliente inativo")] Inactive = 2
    }

    public sealed class Customer
    {
        [CsvColumn("Código", Order = 1)]
        public int Id { get; set; }

        [CsvColumn("Nome", Order = 2)]
        public string Name { get; set; } = string.Empty;

        [CsvColumn("Nascimento", Order = 3, Format = "dd/MM/yyyy")]
        public DateOnly BirthDate { get; set; }

        [CsvColumn("Saldo", Order = 4)]
        public decimal Balance { get; set; }

        [CsvColumn("Situação", Order = 5)]
        public CustomerStatus Status { get; set; }

        [CsvColumn("VIP", Order = 6, Format = "Sim|Não")]
        public bool IsVip { get; set; }

        [CsvColumn("Observação", Order = 7)]
        public string? Notes { get; set; }

        [CsvIgnore]
        public string Ignored { get; set; } = "x";
    }

    private static readonly Customer[] Customers =
    [
        new() { Id = 1, Name = "João da Silva", BirthDate = new DateOnly(1990, 5, 20), Balance = 1234.56m, Status = CustomerStatus.Active, IsVip = true, Notes = "Texto; com separador" },
        new() { Id = 2, Name = "Maria \"Mari\" Souza", BirthDate = new DateOnly(1985, 12, 1), Balance = -10m, Status = CustomerStatus.Inactive, Notes = "Linha 1\r\nLinha 2" },
        new() { Id = 3, Name = "Zé", BirthDate = new DateOnly(2000, 1, 1), Balance = 0m, Status = CustomerStatus.Active, Notes = null }
    ];

    private static async Task<string> WriteToStringAsync<T>(IEnumerable<T> items, CsvOptions? options = null)
    {
        using var stream = new MemoryStream();
        await new CsvWriter(options).WriteAsync(stream, items);
        return Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿');
    }

    private static Task<List<T>> ReadFromStringAsync<T>(string csv, CsvOptions? options = null) where T : new() =>
        new CsvReader(options).ReadAsync<T>(new MemoryStream(Encoding.UTF8.GetBytes(csv))).ToListAsync().AsTask();

    [Test]
    public async Task Write_ProducesExpectedCsv()
    {
        var csv = await WriteToStringAsync(Customers.Take(1));

        await Assert.That(csv).IsEqualTo(
            "Código;Nome;Nascimento;Saldo;Situação;VIP;Observação\r\n" +
            "1;João da Silva;20/05/1990;1234,56;Active;Sim;\"Texto; com separador\"\r\n");
    }

    [Test]
    public async Task Write_EmitsUtf8Bom()
    {
        using var stream = new MemoryStream();
        await new CsvWriter().WriteAsync(stream, Customers);
        await Assert.That(stream.ToArray()[..3]).IsEquivalentTo(new byte[] { 0xEF, 0xBB, 0xBF }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task WriteAndRead_RoundTrip()
    {
        var csv = await WriteToStringAsync(Customers);
        var result = await ReadFromStringAsync<Customer>(csv);

        await Assert.That(result.Count).IsEqualTo(3);
        for (int i = 0; i < Customers.Length; i++)
        {
            await Assert.That(result[i].Id).IsEqualTo(Customers[i].Id);
            await Assert.That(result[i].Name).IsEqualTo(Customers[i].Name);
            await Assert.That(result[i].BirthDate).IsEqualTo(Customers[i].BirthDate);
            await Assert.That(result[i].Balance).IsEqualTo(Customers[i].Balance);
            await Assert.That(result[i].Status).IsEqualTo(Customers[i].Status);
            await Assert.That(result[i].IsVip).IsEqualTo(Customers[i].IsVip);
            await Assert.That(result[i].Notes).IsEqualTo(Customers[i].Notes);
        }
    }

    [Test]
    public async Task Write_EnumAsDescription_AndReadBack()
    {
        var options = new CsvOptions { EnumFormat = CsvEnumFormat.Description };
        var csv = await WriteToStringAsync(Customers, options);

        await Assert.That(csv).Contains("Cliente inativo");
        var result = await ReadFromStringAsync<Customer>(csv);
        await Assert.That(result[1].Status).IsEqualTo(CustomerStatus.Inactive);
    }

    [Test]
    public async Task Read_HeaderIgnoresCaseAccentsOrderAndExtraColumns()
    {
        const string csv = """
            EXTRA;situacao;codigo;NOME
            x;2;10;Ana

            y;Cliente ativo;11;Bruno
            """;

        var result = await ReadFromStringAsync<Customer>(csv);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That((result[0].Id, result[0].Name, result[0].Status)).IsEqualTo((10, "Ana", CustomerStatus.Inactive));
        await Assert.That((result[1].Id, result[1].Name, result[1].Status)).IsEqualTo((11, "Bruno", CustomerStatus.Active));
    }

    [Test]
    public async Task Read_WithoutHeader_MapsByOrder()
    {
        var result = await ReadFromStringAsync<Customer>("5;Carlos;01/02/2003;10,5;Active;Não;obs", new CsvOptions { HasHeader = false });

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Balance).IsEqualTo(10.5m);
        await Assert.That(result[0].IsVip).IsFalse();
    }

    [Test]
    public async Task Read_InvalidValue_ThrowsWithLineAndColumn()
    {
        const string csv = "Código;Nome\n1;Ok\nabc;Erro";

        var ex = await Assert.That(async () => { await ReadFromStringAsync<Customer>(csv); }).ThrowsExactly<CsvException>();

        await Assert.That(ex!.LineNumber).IsEqualTo(3);
        await Assert.That(ex.ColumnName).IsEqualTo("Código");
    }

    [Test]
    public async Task Read_SkipInvalidRows_ReportsErrors()
    {
        var errors = new List<CsvException>();
        var options = new CsvOptions { SkipInvalidRows = true, OnInvalidRow = errors.Add };

        var result = await ReadFromStringAsync<Customer>("Código;Nome\n1;Ok\nabc;Erro\n3;Ok", options);

        await Assert.That(result.Select(c => c.Id)).IsEquivalentTo(new[] { 1, 3 }, CollectionOrdering.Matching);
        await Assert.That(errors).HasSingleItem();
    }

    [Test]
    public async Task Read_UnterminatedQuote_Throws()
    {
        await Assert.That(async () => { await ReadFromStringAsync<Customer>("Código;Nome\n1;\"sem fim"); }).ThrowsExactly<CsvException>();
    }

    [Test]
    public async Task SanitizeFormulas_PrefixesDangerousValues()
    {
        var csv = await WriteToStringAsync([new Customer { Name = "=HYPERLINK(\"x\")" }], new CsvOptions { SanitizeFormulas = true });
        await Assert.That(csv).Contains("\"'=HYPERLINK(\"\"x\"\")\"");
    }

    [Test]
    public async Task LargeFile_StreamsWithoutLoadingAll()
    {
        const int total = 100_000;
        var path = Path.Combine(Path.GetTempPath(), $"tec-core-{Guid.NewGuid():N}.csv");
        try
        {
            await new CsvWriter().WriteFileAsync(path, GenerateAsync(total));

            long count = 0;
            decimal sum = 0;
            await foreach (var customer in new CsvReader().ReadFileAsync<Customer>(path))
            {
                count++;
                sum += customer.Balance;
            }

            await Assert.That(count).IsEqualTo(total);
            await Assert.That(sum).IsEqualTo(total * 1.5m);
        }
        finally
        {
            File.Delete(path);
        }
    }

    public struct PointStruct
    {
        public int X { get; set; }
        public string? Label { get; set; }
    }

    public class BaseWithIgnore
    {
        public int Id { get; set; }

        [CsvIgnore]
        public virtual string Secret { get; set; } = "segredo";

        [CsvColumn("Nome Completo")]
        public virtual string Name { get; set; } = string.Empty;
    }

    public sealed class DerivedOverride : BaseWithIgnore
    {
        public override string Secret { get; set; } = "segredo";

        public override string Name { get; set; } = string.Empty;
    }

    public sealed class Amount
    {
        public string? Name { get; set; }
        public decimal Value { get; set; }
        public double Rate { get; set; }
    }

    // Regressão: com T struct, SetValue alterava uma cópia em caixa e o leitor devolvia valores padrão
    [Test]
    public async Task Read_StructType_PopulatesValues()
    {
        var result = await ReadFromStringAsync<PointStruct>("X;Label\n7;sete\n8;oito");

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That((result[0].X, result[0].Label)).IsEqualTo((7, "sete"));
        await Assert.That((result[1].X, result[1].Label)).IsEqualTo((8, "oito"));
    }

    // Regressão: [CsvIgnore]/[CsvColumn] na classe base eram perdidos em propriedades sobrescritas (override)
    [Test]
    public async Task Attributes_AreInherited_ByOverriddenProperties()
    {
        var csv = await WriteToStringAsync([new DerivedOverride { Id = 1, Name = "Ana" }]);

        await Assert.That(csv).IsEqualTo("Id;Nome Completo\r\n1;Ana\r\n");
        await Assert.That(csv).DoesNotContain("segredo");
    }

    // Regressão: em pt-BR "1.5" virava 15 e "1.2.3" virava 123
    [Test]
    [Arguments("1.5")]
    [Arguments("1.2.3")]
    [Arguments("12.34")]
    public async Task Read_InvalidThousandsGrouping_IsRejected(string value)
    {
        var ex = await Assert.That(async () => { await ReadFromStringAsync<Amount>($"Value\n{value}"); }).ThrowsExactly<CsvException>();
        await Assert.That(ex!.ColumnName).IsEqualTo("Value");

        await Assert.That(async () => { await ReadFromStringAsync<Amount>($"Rate\n{value}"); }).ThrowsExactly<CsvException>();
    }

    [Test]
    public async Task Read_ValidThousandsGrouping_IsAccepted_AndCanBeDisabled()
    {
        var result = await ReadFromStringAsync<Amount>("Value;Rate\n1.234.567,89;1.234,5\n10,5;0,25");

        await Assert.That(result[0].Value).IsEqualTo(1234567.89m);
        await Assert.That(result[0].Rate).IsEqualTo(1234.5);
        await Assert.That(result[1].Value).IsEqualTo(10.5m);

        var noThousands = new CsvOptions { AllowThousandsSeparator = false };
        await Assert.That(async () => { await ReadFromStringAsync<Amount>("Value\n1.234,5", noThousands); }).ThrowsExactly<CsvException>();
        await Assert.That((await ReadFromStringAsync<Amount>("Value\n1234,5", noThousands))[0].Value).IsEqualTo(1234.5m);
    }

    // Regressão: o writer coloca aspas para preservar espaços nas pontas, mas o reader (TrimValues) os removia
    [Test]
    public async Task WriteAndRead_PreservesLeadingAndTrailingSpaces()
    {
        var csv = await WriteToStringAsync([new Amount { Name = "  com espaços  ", Value = 1 }]);
        var result = await ReadFromStringAsync<Amount>(csv);

        await Assert.That(result[0].Name).IsEqualTo("  com espaços  ");

        // Sem aspas, o TrimValues continua aparando
        var unquoted = await ReadFromStringAsync<Amount>("Name;Value\n  sem aspas  ;1");
        await Assert.That(unquoted[0].Name).IsEqualTo("sem aspas");
    }

    // Regressão: espaço antes da aspa fazia o separador dentro das aspas quebrar o campo
    [Test]
    public async Task Read_SpaceBeforeOpeningQuote_KeepsQuotedField()
    {
        var result = await ReadFromStringAsync<Amount>("Name;Value;Rate\n \"a;b\" ; 2 ;3");

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].Name).IsEqualTo("a;b");
        await Assert.That(result[0].Value).IsEqualTo(2m);
        await Assert.That(result[0].Rate).IsEqualTo(3d);
    }

    [Test]
    public async Task Read_ShortRow_DefaultIsLenient_StrictThrows()
    {
        const string csv = "Código;Nome;Saldo\n1;Ana\n2;Bia;5";

        var lenient = await ReadFromStringAsync<Customer>(csv);
        await Assert.That(lenient.Count).IsEqualTo(2);
        await Assert.That(lenient[0].Balance).IsEqualTo(0m);

        var strict = new CsvOptions { StrictColumnCount = true };
        var ex = await Assert.That(async () => { await ReadFromStringAsync<Customer>(csv, strict); }).ThrowsExactly<CsvException>();
        await Assert.That(ex!.LineNumber).IsEqualTo(2);
        await Assert.That(ex.Message).Contains("esperado 3, encontrado 2");

        var errors = new List<CsvException>();
        var skipping = new CsvOptions { StrictColumnCount = true, SkipInvalidRows = true, OnInvalidRow = errors.Add };
        var kept = await ReadFromStringAsync<Customer>(csv, skipping);
        await Assert.That(kept.Select(c => c.Id)).IsEquivalentTo(new[] { 2 }, CollectionOrdering.Matching);
        await Assert.That(errors).HasSingleItem();
    }

    [Test]
    public async Task Read_StrictColumnCount_WithoutHeader_UsesMappedColumns()
    {
        var options = new CsvOptions { HasHeader = false, StrictColumnCount = true };

        await Assert.That(async () => { await ReadFromStringAsync<Customer>("5;Carlos", options); }).ThrowsExactly<CsvException>();
        var ok = await ReadFromStringAsync<Customer>("5;Carlos;01/02/2003;10,5;Active;Não;obs", options);
        await Assert.That(ok).HasSingleItem();
    }

    [Test]
    public async Task Read_RequireAllColumns_ListsMissingColumns()
    {
        var options = new CsvOptions { RequireAllColumns = true };

        var ex = await Assert.That(async () => { await ReadFromStringAsync<Amount>("Name;Value\nA;1", options); }).ThrowsExactly<CsvException>();
        await Assert.That(ex!.Message).Contains("Rate");
        await Assert.That(ex.LineNumber).IsEqualTo(1);

        var ok = await ReadFromStringAsync<Amount>("Rate;Name;Value\n1;A;2", options);
        await Assert.That(ok).HasSingleItem();
    }

    [Test]
    public async Task Read_RecordAboveMaxRecordLength_Throws()
    {
        var options = new CsvOptions { MaxRecordLength = 20 };
        var csv = "Name;Value\n" + string.Join(';', Enumerable.Repeat("abcde", 6)) + "\nA;1";

        var ex = await Assert.That(async () => { await ReadFromStringAsync<Amount>(csv, options); }).ThrowsExactly<CsvException>();
        await Assert.That(ex!.LineNumber).IsEqualTo(2);
        await Assert.That(new CsvOptions().MaxRecordLength).IsEqualTo(4 * 1024 * 1024);
        await Assert.That(() => new CsvOptions { MaxRecordLength = 0 }).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    private static async IAsyncEnumerable<Customer> GenerateAsync(int total)
    {
        for (int i = 1; i <= total; i++)
        {
            yield return new Customer { Id = i, Name = $"Cliente {i}", BirthDate = new DateOnly(2000, 1, 1), Balance = 1.5m, Status = CustomerStatus.Active };
            if (i % 10_000 == 0)
                await Task.Yield();
        }
    }
}
