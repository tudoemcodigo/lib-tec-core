using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using TEC.Core.Common.Guards;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Csv.Internal;

namespace TEC.Core.Csv;

/// <summary>
/// Leitura de CSV para objetos tipados, em streaming.
/// </summary>
/// <remarks>
/// Com cabeçalho, as colunas são associadas às propriedades pelo nome (sem diferenciar maiúsculas e acentos);
/// colunas extras são ignoradas. Sem cabeçalho, a associação é feita pela ordem das propriedades.
/// Por padrão, colunas ausentes no cabeçalho e linhas com menos colunas deixam as propriedades com o valor padrão;
/// use <see cref="CsvOptions.RequireAllColumns"/> e <see cref="CsvOptions.StrictColumnCount"/> para recusá-las.
/// </remarks>
public sealed class CsvReader : ICsvReader
{
    private readonly CsvOptions _options;

    /// <summary>Cria o leitor com as opções informadas (ou as padrão).</summary>
    public CsvReader(CsvOptions? options = null)
    {
        _options = options ?? CsvOptions.Default;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ReadFileAsync<[DynamicallyAccessedMembers(CsvTypeMap.MappedMembers)] T>(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : new()
    {
        Guard.NotNullOrWhiteSpace(filePath);
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using (stream.ConfigureAwait(false))
        {
            await foreach (var item in ReadAsync<T>(stream, cancellationToken).ConfigureAwait(false))
                yield return item;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ReadAsync<[DynamicallyAccessedMembers(CsvTypeMap.MappedMembers)] T>(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : new()
    {
        Guard.NotNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("O stream não permite leitura.", nameof(stream));
        using var textReader = new StreamReader(stream, _options.Encoding, detectEncodingFromByteOrderMarks: true,
            bufferSize: _options.BufferSize, leaveOpen: true);

        var parser = new CsvRecordParser(textReader, _options.Delimiter, _options.BufferSize, _options.MaxFieldLength,
            _options.MaxColumns, _options.MaxRecordLength);
        var typeMap = CsvTypeMap.For(typeof(T));
        (CsvColumn Column, int Index)[]? bindings = _options.HasHeader ? null : BindByOrder(typeMap);
        int expectedColumns = typeMap.Columns.Count;

        await foreach (var record in parser.ReadRecordsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (record.IsEmpty && _options.SkipEmptyLines)
                continue;

            if (bindings is null)
            {
                bindings = BindByHeader(typeMap, record, _options.RequireAllColumns, _options.SanitizeFormulas);
                expectedColumns = record.Fields.Length;
                continue;
            }

            if (TryMap(record, bindings, expectedColumns, out T item))
                yield return item;
        }
    }

    private bool TryMap<T>(CsvRecord record, (CsvColumn Column, int Index)[] bindings, int expectedColumns, out T item) where T : new()
    {
        item = default!;

        if (_options.StrictColumnCount && record.Fields.Length != expectedColumns)
        {
            return Reject(new CsvException(
                $"Quantidade de colunas inválida: esperado {expectedColumns}, encontrado {record.Fields.Length}", record.LineNumber));
        }

        // Instância em caixa (boxed): com T struct, SetValue em "item" alteraria apenas uma cópia temporária
        object target = new T();
        foreach (var (column, index) in bindings)
        {
            if (index >= record.Fields.Length)
                continue;

            var raw = record.Fields[index];
            object? value;
            try
            {
                value = CsvValueConverter.FromString(raw, column.PropertyType, column.Format, _options,
                    column.IsNullableString, record.IsQuoted(index));
            }
            catch (Exception ex) when (IsValueError(ex))
            {
                return Reject(InvalidValue($"Valor inválido para o tipo {column.PropertyType.Name}", record, column, raw, ex));
            }

            try
            {
                column.Property.SetValue(target, value);
            }
            // Exceção lançada pelo setter (ex.: validação no init) chega embrulhada pela reflexão
            catch (TargetInvocationException ex) when (ex.InnerException is { } inner && IsValueError(inner))
            {
                return Reject(InvalidValue($"Valor recusado pela propriedade {column.Property.Name}", record, column, raw, inner));
            }
            catch (Exception ex) when (IsValueError(ex))
            {
                return Reject(InvalidValue($"Valor inválido para o tipo {column.PropertyType.Name}", record, column, raw, ex));
            }
        }

        item = (T)target;
        return true;
    }

    private static bool IsValueError(Exception ex) =>
        ex is FormatException or InvalidCastException or OverflowException or ArgumentException or NotSupportedException;

    // O valor original só é exposto se habilitado explicitamente (evita vazar dados pessoais em logs)
    private CsvException InvalidValue(string message, CsvRecord record, CsvColumn column, string raw, Exception cause)
    {
        bool includeRaw = _options.IncludeRawValueInErrors;
        return new CsvException(message, record.LineNumber, column.Name, includeRaw ? raw : null, includeRaw ? cause : null);
    }

    // Linha inválida: lança ou, com SkipInvalidRows, notifica e descarta
    private bool Reject(CsvException error)
    {
        if (!_options.SkipInvalidRows)
            throw error;

        _options.OnInvalidRow?.Invoke(error);
        return false;
    }

    private static (CsvColumn, int)[] BindByHeader(CsvTypeMap typeMap, CsvRecord header, bool requireAllColumns, bool sanitizeFormulas)
    {
        // O apóstrofo anti-fórmula do CsvWriter ("'=Total") é removido antes de comparar os nomes
        var normalizedHeader = header.Fields
            .Select(name => CsvTypeMap.NormalizeName(sanitizeFormulas ? CsvFormulaGuard.Unsanitize(name) : name))
            .ToArray();
        var bindings = new List<(CsvColumn, int)>();
        var missing = new List<string>();

        foreach (var column in typeMap.Columns.Where(c => c.CanWrite))
        {
            int index = Array.IndexOf(normalizedHeader, CsvTypeMap.NormalizeName(column.Name));
            if (index >= 0)
                bindings.Add((column, index));
            else
                missing.Add(column.Name);
        }

        if (bindings.Count == 0)
            throw new CsvException("Nenhuma coluna do cabeçalho corresponde às propriedades do tipo de destino", header.LineNumber);

        if (requireAllColumns && missing.Count > 0)
            throw new CsvException($"Colunas obrigatórias ausentes no cabeçalho: {string.Join(", ", missing)}", header.LineNumber);

        return [.. bindings];
    }

    private static (CsvColumn, int)[] BindByOrder(CsvTypeMap typeMap) =>
        typeMap.Columns
            .Select((column, index) => (column, index))
            .Where(x => x.column.CanWrite)
            .ToArray();

}
