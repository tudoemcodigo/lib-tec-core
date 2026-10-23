using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using TEC.Core.Common.Guards;
using TEC.Core.Dates.Holidays.Internal;

namespace TEC.Core.Dates.Holidays.Sources;

/// <summary>
/// Feriados lidos de um banco de dados via ADO.NET (<see cref="DbConnection"/>), sem depender de ORM ou de provedor específico.
/// </summary>
/// <remarks>
/// As colunas são associadas pelo nome, sem diferenciar maiúsculas, acentos e <c>_</c>:
/// <list type="bullet">
///   <item><c>Data</c> (obrigatória): <c>date</c>/<c>datetime</c>, ou texto em <c>aaaa-MM-dd</c>/<c>dd/MM/aaaa</c>.</item>
///   <item><c>Descricao</c> (obrigatória).</item>
///   <item><c>Uf</c> e <c>CodigoIbge</c> (opcionais; <c>NULL</c> nos feriados nacionais).</item>
/// </list>
/// Use alias no SQL quando a tabela tiver outros nomes (<c>SELECT dt_feriado AS Data, ...</c>).
/// A conexão é criada pela função informada e descartada ao final da leitura.
/// </remarks>
/// <example>
/// <code>
/// var source = new DbHolidaySource(
///     () => new SqlConnection(connectionString),
///     "SELECT Data, Descricao, Uf, CodigoIbge FROM Feriados WHERE Ativo = 1");
/// </code>
/// </example>
public sealed class DbHolidaySource : IHolidaySource
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _commandText;
    private readonly Action<DbCommand>? _configureCommand;

    /// <summary>Cria a fonte.</summary>
    /// <param name="connectionFactory">Cria uma conexão nova (aberta ou não). A fonte a descarta ao final.</param>
    /// <param name="commandText">
    /// SQL fixo definido pela aplicação. <b>Nunca</b> concatene valores externos: use parâmetros em <paramref name="configureCommand"/>.
    /// </param>
    /// <param name="configureCommand">Ajustes no comando antes da execução (parâmetros, timeout…).</param>
    /// <param name="name">Nome da fonte em mensagens de erro (padrão: <c>"Banco de dados"</c>). Não inclua a connection string.</param>
    public DbHolidaySource(Func<DbConnection> connectionFactory, string commandText, Action<DbCommand>? configureCommand = null, string? name = null)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
        _commandText = Guard.NotNullOrWhiteSpace(commandText);
        _configureCommand = configureCommand;
        Name = string.IsNullOrWhiteSpace(name) ? "Banco de dados" : name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "O SQL é definido pela aplicação na configuração (não vem do usuário final); valores entram por parâmetros em configureCommand.")]
    public async IAsyncEnumerable<Holiday> LoadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connection = _connectionFactory() ?? throw new InvalidOperationException("A função de conexão retornou null.");
        await using (connection.ConfigureAwait(false))
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = _commandText;
                _configureCommand?.Invoke(command);

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var columns = ColumnMap.From(reader);
                    int row = 0;
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        row++;
                        yield return Map(reader, columns, row);
                    }
                }
            }
        }
    }

    private static Holiday Map(DbDataReader reader, ColumnMap columns, int row)
    {
        try
        {
            var date = ToDate(reader.GetValue(columns.Date));
            var description = reader.IsDBNull(columns.Description)
                ? throw new FormatException("Descrição nula.")
                : Convert.ToString(reader.GetValue(columns.Description), CultureInfo.InvariantCulture)!;
            string? uf = columns.Uf is int ufIndex && !reader.IsDBNull(ufIndex)
                ? Convert.ToString(reader.GetValue(ufIndex), CultureInfo.InvariantCulture)
                : null;
            int? ibgeCode = columns.IbgeCode is int ibgeIndex && !reader.IsDBNull(ibgeIndex)
                ? ToIbgeCode(reader.GetValue(ibgeIndex))
                : null;

            return new Holiday(date, description) { Uf = uf, IbgeCode = ibgeCode };
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidCastException or OverflowException)
        {
            throw new FormatException($"Feriado na linha {row} do resultado: {ex.Message}", ex);
        }
    }

    private static DateOnly ToDate(object value) => value switch
    {
        DateOnly date => date,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        DateTimeOffset offset => DateOnly.FromDateTime(offset.DateTime),
        string text => HolidayFields.ParseDate(text),
        DBNull => throw new FormatException("Data nula."),
        _ => throw new FormatException("Tipo de coluna não suportado para a data."),
    };

    private static int ToIbgeCode(object value) => value switch
    {
        int code => code,
        long or short or byte or sbyte or ushort or uint or ulong => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        // NUMBER (Oracle), REAL (SQLite) e colunas calculadas: só valores inteiros, sem arredondar ("3550308.5" é recusado)
        decimal number => decimal.IsInteger(number) ? decimal.ToInt32(number) : throw FractionalIbgeCode(),
        double number => double.IsInteger(number) ? checked((int)number) : throw FractionalIbgeCode(),
        float number => float.IsInteger(number) ? checked((int)number) : throw FractionalIbgeCode(),
        string text => HolidayFields.ParseIbgeCode(text) ?? throw new FormatException("Código IBGE vazio."),
        _ => throw new FormatException("Tipo de coluna não suportado para o código IBGE."),
    };

    private static FormatException FractionalIbgeCode() => new("Código IBGE inválido: valor não inteiro.");

    private sealed record ColumnMap(int Date, int Description, int? Uf, int? IbgeCode)
    {
        public static ColumnMap From(DbDataReader reader)
        {
            int? date = null, description = null, uf = null, ibgeCode = null;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                switch (HolidayFields.Resolve(reader.GetName(i)))
                {
                    case HolidayField.Date: date ??= i; break;
                    case HolidayField.Description: description ??= i; break;
                    case HolidayField.Uf: uf ??= i; break;
                    case HolidayField.IbgeCode: ibgeCode ??= i; break;
                }
            }

            if (date is null || description is null)
                throw new InvalidOperationException("O resultado do SQL de feriados precisa das colunas 'Data' e 'Descricao' (use alias se necessário).");

            return new ColumnMap(date.Value, description.Value, uf, ibgeCode);
        }
    }
}
