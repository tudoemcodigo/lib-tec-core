using System.Diagnostics.CodeAnalysis;
using System.Text;
using TEC.Core.Common.Guards;
using TEC.Core.Csv.Abstractions;
using TEC.Core.Csv.Internal;

namespace TEC.Core.Csv;

/// <summary>
/// Escrita de coleções de objetos em CSV, em streaming.
/// </summary>
/// <remarks>
/// Colunas: propriedades públicas com getter, ordenadas por <c>[CsvColumn(Order)]</c> e depois pela declaração.
/// Campos que contêm separador, aspas, quebra de linha ou espaços nas pontas são envolvidos em aspas automaticamente
/// (o <see cref="CsvReader"/> não apara campos entre aspas, preservando esses espaços na leitura).
/// </remarks>
public sealed class CsvWriter : ICsvWriter
{
    private readonly CsvOptions _options;

    /// <summary>Cria o escritor com as opções informadas (ou as padrão).</summary>
    public CsvWriter(CsvOptions? options = null)
    {
        _options = options ?? CsvOptions.Default;
    }

    /// <inheritdoc />
    public Task WriteAsync<[DynamicallyAccessedMembers(CsvTypeMap.MappedMembers)] T>(Stream stream, IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(items);
        return WriteAsync(stream, ToAsyncEnumerable(items), cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteAsync<[DynamicallyAccessedMembers(CsvTypeMap.MappedMembers)] T>(Stream stream, IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);
        if (!stream.CanWrite)
            throw new ArgumentException("O stream não permite escrita.", nameof(stream));
        Guard.NotNull(items);

        var columns = CsvTypeMap.For(typeof(T)).Columns;
        var writer = new StreamWriter(stream, _options.Encoding, _options.BufferSize, leaveOpen: true)
        {
            NewLine = _options.NewLine
        };
        await using var writerScope = writer.ConfigureAwait(false);
        var line = new StringBuilder(256);

        if (_options.HasHeader)
        {
            // Nomes de coluna também são neutralizados: um cabeçalho "=Total" seria executado como fórmula na planilha
            AppendLine(line, columns.Select(c => _options.SanitizeFormulas ? CsvFormulaGuard.Sanitize(c.Name) : c.Name));
            await writer.WriteLineAsync(line, cancellationToken).ConfigureAwait(false);
        }

        await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (item is null)
                continue;

            AppendLine(line, columns.Select(c => FormatValue(c, item)));
            await writer.WriteLineAsync(line, cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task WriteFileAsync<[DynamicallyAccessedMembers(CsvTypeMap.MappedMembers)] T>(string filePath, IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(items);
        return WriteFileAsync(filePath, ToAsyncEnumerable(items), cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteFileAsync<[DynamicallyAccessedMembers(CsvTypeMap.MappedMembers)] T>(string filePath, IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(filePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.Asynchronous);
        await using var streamScope = stream.ConfigureAwait(false);
        await WriteAsync(stream, items, cancellationToken).ConfigureAwait(false);
    }

    private void AppendLine(StringBuilder line, IEnumerable<string> values)
    {
        line.Clear();
        bool first = true;
        foreach (var value in values)
        {
            if (!first)
                line.Append(_options.Delimiter);

            AppendField(line, value);
            first = false;
        }
    }

    // Valores textuais são neutralizados contra fórmulas; números negativos, datas, booleanos e enums permanecem intactos
    private string FormatValue(CsvColumn column, object item)
    {
        var value = column.Property.GetValue(item);
        var text = CsvValueConverter.ToString(value, column.Format, _options);
        return _options.SanitizeFormulas && CsvValueConverter.IsFreeText(value) ? CsvFormulaGuard.Sanitize(text) : text;
    }

    private void AppendField(StringBuilder line, string value)
    {
        bool mustQuote = _options.QuoteAllFields
            || value.Contains(_options.Delimiter)
            || value.AsSpan().IndexOfAny('"', '\r', '\n') >= 0
            || (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])));

        if (!mustQuote)
        {
            line.Append(value);
            return;
        }

        line.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
    }

#pragma warning disable CS1998 // Iterador assíncrono sem await: adapta IEnumerable para IAsyncEnumerable
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
            yield return item;
    }
#pragma warning restore CS1998
}
