using System.Runtime.CompilerServices;
using System.Text;

namespace TEC.Core.Csv.Internal;

/// <summary>
/// Parser de CSV compatível com a RFC 4180: campos entre aspas, aspas escapadas ("")
/// e quebras de linha dentro de campos. Lê em blocos, sem carregar o arquivo inteiro.
/// O tamanho de cada campo, de cada registro e a quantidade de colunas são limitados (o arquivo é entrada não confiável).
/// </summary>
/// <remarks>
/// Tolerâncias além da RFC: espaços/TAB antes da aspa de abertura e depois da aspa de fechamento são ignorados
/// (<c>a; "b;c" ;d</c> tem três campos: <c>a</c>, <c>b;c</c> e <c>d</c>), e cada campo informa se estava entre aspas,
/// para que o conteúdo entre aspas não seja aparado na conversão.
/// </remarks>
internal sealed class CsvRecordParser(TextReader reader, char delimiter, int bufferSize, int maxFieldLength, int maxColumns, int maxRecordLength)
{
    private enum State
    {
        FieldStart,
        LeadingWhitespace,
        Unquoted,
        Quoted,
        QuoteInQuoted,
        AfterQuoted
    }

    public async IAsyncEnumerable<CsvRecord> ReadRecordsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new char[bufferSize];
        var fields = new List<string>();
        var quotedFields = new List<bool>();
        var field = new StringBuilder();
        var state = State.FieldStart;
        bool lastWasCarriageReturn = false;
        bool recordHasQuotes = false;
        bool fieldQuoted = false;
        long recordLength = 0;
        long line = 1;
        long recordStartLine = 1;

        int length;
        while ((length = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < length; i++)
            {
                char c = buffer[i];

                // Trata \r\n como uma única quebra de linha
                if (lastWasCarriageReturn)
                {
                    lastWasCarriageReturn = false;
                    if (c == '\n' && state == State.FieldStart)
                        continue;
                }

                if (++recordLength > maxRecordLength)
                    throw new CsvException($"Registro excede o tamanho máximo de {maxRecordLength} caracteres", recordStartLine);

                switch (state)
                {
                    case State.FieldStart or State.LeadingWhitespace when c == '"':
                        // Espaços antes da aspa de abertura não fazem parte do campo
                        field.Clear();
                        state = State.Quoted;
                        recordHasQuotes = fieldQuoted = true;
                        break;

                    case State.FieldStart or State.LeadingWhitespace when c != delimiter && c is ' ' or '\t':
                        field.Append(c);
                        state = State.LeadingWhitespace;
                        break;

                    case State.Quoted:
                        if (c == '"')
                        {
                            state = State.QuoteInQuoted;
                        }
                        else
                        {
                            field.Append(c);
                            if (c == '\n')
                                line++;
                        }
                        break;

                    case State.QuoteInQuoted when c == '"':
                        // Aspas duplicadas dentro de campo entre aspas representam uma aspa literal
                        field.Append('"');
                        state = State.Quoted;
                        break;

                    case State.QuoteInQuoted or State.AfterQuoted when c != delimiter && c is ' ' or '\t':
                        // Espaços depois da aspa de fechamento são ignorados
                        state = State.AfterQuoted;
                        break;

                    default:
                        // FieldStart (sem aspas), Unquoted ou após fechar aspas
                        if (c == delimiter)
                        {
                            EndField(fields, quotedFields, field, ref fieldQuoted, recordStartLine);
                            state = State.FieldStart;
                        }
                        else if (c is '\r' or '\n')
                        {
                            EndField(fields, quotedFields, field, ref fieldQuoted, recordStartLine);
                            yield return BuildRecord(fields, quotedFields, recordStartLine, recordHasQuotes);

                            fields.Clear();
                            quotedFields.Clear();
                            recordHasQuotes = false;
                            recordLength = 0;
                            state = State.FieldStart;
                            lastWasCarriageReturn = c == '\r';
                            recordStartLine = ++line;
                        }
                        else
                        {
                            field.Append(c);
                            state = State.Unquoted;
                        }
                        break;
                }

                // Limites contra arquivos maliciosos (esgotamento de memória)
                if (field.Length > maxFieldLength)
                    throw new CsvException($"Campo excede o tamanho máximo de {maxFieldLength} caracteres", recordStartLine);
            }
        }

        if (state == State.Quoted)
            throw new CsvException("Campo entre aspas não foi finalizado", recordStartLine);

        // Último registro sem quebra de linha no final do arquivo
        if (fields.Count > 0 || field.Length > 0 || recordHasQuotes)
        {
            EndField(fields, quotedFields, field, ref fieldQuoted, recordStartLine);
            yield return BuildRecord(fields, quotedFields, recordStartLine, recordHasQuotes);
        }
    }

    // O limite de colunas é conferido a cada campo fechado, inclusive o último da linha (fechado por quebra de linha ou fim do arquivo)
    private void EndField(List<string> fields, List<bool> quotedFields, StringBuilder field, ref bool fieldQuoted, long recordStartLine)
    {
        if (fields.Count >= maxColumns)
            throw new CsvException($"Linha excede o máximo de {maxColumns} colunas", recordStartLine);

        fields.Add(field.ToString());
        quotedFields.Add(fieldQuoted);
        field.Clear();
        fieldQuoted = false;
    }

    private static CsvRecord BuildRecord(List<string> fields, List<bool> quotedFields, long lineNumber, bool hasQuotes)
    {
        bool isEmpty = !hasQuotes && fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]);
        return new CsvRecord([.. fields], lineNumber, isEmpty, hasQuotes ? [.. quotedFields] : null);
    }
}
