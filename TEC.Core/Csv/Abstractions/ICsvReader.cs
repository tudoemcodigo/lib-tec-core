using System.Diagnostics.CodeAnalysis;

namespace TEC.Core.Csv.Abstractions;

/// <summary>
/// Leitura de CSV para objetos tipados, em streaming (não carrega o arquivo inteiro em memória).
/// </summary>
public interface ICsvReader
{
    /// <summary>
    /// Lê os registros do stream sob demanda.
    /// Para obter uma lista, use <c>await reader.ReadAsync&lt;T&gt;(stream).ToListAsync()</c>.
    /// </summary>
    IAsyncEnumerable<T> ReadAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(Stream stream, CancellationToken cancellationToken = default) where T : new();

    /// <summary>Lê os registros de um arquivo sob demanda.</summary>
    IAsyncEnumerable<T> ReadFileAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string filePath, CancellationToken cancellationToken = default) where T : new();
}
