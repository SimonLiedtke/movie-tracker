using System.Text;

namespace MovieTracker.Infrastructure.Letterboxd;

/// <summary>One CSV row, addressed by the column names of the header row.</summary>
internal sealed class CsvRecord(IReadOnlyDictionary<string, string> values)
{
    /// <summary>The value of the column, or an empty string if the file has no such column.</summary>
    public string this[string column] => values.GetValueOrDefault(column, string.Empty);
}

/// <summary>
/// Reads CSV as Letterboxd writes it (RFC 4180): fields in double quotes may contain commas, line breaks
/// and doubled quotes. The first row is the header.
/// </summary>
internal static class CsvReader
{
    public static IEnumerable<CsvRecord> ReadRecords(TextReader reader)
    {
        string[]? header = null;

        foreach (var row in ReadRows(reader))
        {
            if (header is null)
            {
                header = [.. row.Select(name => name.Trim())];
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Length && i < row.Count; i++)
            {
                values[header[i]] = row[i];
            }

            yield return new CsvRecord(values);
        }
    }

    private static IEnumerable<List<string>> ReadRows(TextReader reader)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        int next;
        while ((next = reader.Read()) != -1)
        {
            var character = (char)next;

            if (inQuotes)
            {
                if (character != '"')
                {
                    field.Append(character);
                }
                else if (reader.Peek() == '"')
                {
                    reader.Read();
                    field.Append('"');
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r' or '\n':
                    if (character == '\r' && reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    row.Add(field.ToString());
                    field.Clear();
                    if (!IsBlank(row))
                    {
                        yield return row;
                    }

                    row = [];
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        // The last row may end without a line break.
        row.Add(field.ToString());
        if (!IsBlank(row))
        {
            yield return row;
        }
    }

    private static bool IsBlank(List<string> row) => row.Count == 1 && row[0].Length == 0;
}
