using System.Diagnostics.CodeAnalysis;

namespace TEC.Core.Csv.Abstractions;

/// <summary>
/// Escrita de coleções de objetos em CSV, em streaming.
/// </summary>
public interface ICsvWriter
{
    /// <summary>Escreve os itens no stream. O stream permanece aberto ao final.</summary>
    Task WriteAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(Stream stream, IEnumerable<T> items, CancellationToken cancellationToken = default);

    /// <summary>Escreve os itens (produzidos de forma assíncrona, ex.: consulta ao banco) no stream.</summary>
    Task WriteAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(Stream stream, IAsyncEnumerable<T> items, CancellationToken cancellationToken = default);

    /// <summary>Escreve os itens em um arquivo (sobrescreve se existir).</summary>
    Task WriteFileAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string filePath, IEnumerable<T> items, CancellationToken cancellationToken = default);

    /// <summary>Escreve os itens (produzidos de forma assíncrona) em um arquivo (sobrescreve se existir).</summary>
    Task WriteFileAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string filePath, IAsyncEnumerable<T> items, CancellationToken cancellationToken = default);
}
