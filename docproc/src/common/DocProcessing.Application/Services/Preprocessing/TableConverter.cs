using System.Text;
using System.Text.Json;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Models.Preprocessing;

namespace DocProcessing.Application.Services.Preprocessing;

/// <summary>
/// Provides table conversion services for preprocessing.
/// </summary>
public sealed class TableConverter : ITableConverter
{
    /// <inheritdoc/>
    public StructuredTable ConvertToStructured(TableData table)
    {
        ArgumentNullException.ThrowIfNull(table);

        // Extract headers from cells marked as IsHeader or from first row
        var headers = ExtractHeaders(table);

        // Build rows as dictionaries mapping header -> cell value
        var rows = BuildRows(table, headers);

        // Generate JSON representation
        var jsonRepresentation = JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Generate CSV representation
        var csvRepresentation = BuildCsv(headers, rows);

        return new StructuredTable
        {
            TableNumber = 1, // Will be set by caller with actual table number
            PageNumber = table.PageNumber,
            Headers = headers,
            Rows = rows,
            JsonRepresentation = jsonRepresentation,
            CsvRepresentation = csvRepresentation,
            Confidence = table.Confidence,
            BoundingBox = table.BoundingBox
        };
    }

    private static IReadOnlyList<string> ExtractHeaders(TableData table)
    {
        // Try to find header cells
        var headerCells = table.Cells
            .Where(c => c.IsHeader)
            .OrderBy(c => c.ColumnIndex)
            .ToList();

        if (headerCells.Count > 0)
        {
            return headerCells.Select(c => c.Content).ToList();
        }

        // Fallback: use first row as headers
        var firstRowCells = table.Cells
            .Where(c => c.RowIndex == 0)
            .OrderBy(c => c.ColumnIndex)
            .ToList();

        if (firstRowCells.Count > 0)
        {
            return firstRowCells.Select(c => c.Content).ToList();
        }

        // Fallback: generate column names
        return Enumerable.Range(0, table.ColumnCount)
            .Select(i => $"Column{i + 1}")
            .ToList();
    }

    private static IReadOnlyList<Dictionary<string, string>> BuildRows(TableData table, IReadOnlyList<string> headers)
    {
        var rows = new List<Dictionary<string, string>>();

        // Determine starting row (skip header row if headers came from first row)
        var hasHeaderRow = table.Cells.Any(c => c.IsHeader && c.RowIndex == 0) ||
                          (table.Cells.Any(c => c.RowIndex == 0) && !table.Cells.Any(c => c.IsHeader));

        var startRow = hasHeaderRow ? 1 : 0;

        // Group cells by row
        var cellsByRow = table.Cells
            .Where(c => c.RowIndex >= startRow)
            .GroupBy(c => c.RowIndex)
            .OrderBy(g => g.Key);

        foreach (var rowGroup in cellsByRow)
        {
            var row = new Dictionary<string, string>();
            var cellsInRow = rowGroup.OrderBy(c => c.ColumnIndex).ToList();

            for (int i = 0; i < cellsInRow.Count && i < headers.Count; i++)
            {
                var header = headers[i];
                var cell = cellsInRow[i];

                // Handle column spans by using the same value for multiple headers
                if (cell.ColumnSpan > 1)
                {
                    for (int span = 0; span < cell.ColumnSpan && (i + span) < headers.Count; span++)
                    {
                        row[headers[i + span]] = cell.Content;
                    }
                }
                else
                {
                    row[header] = cell.Content;
                }
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string BuildCsv(IReadOnlyList<string> headers, IReadOnlyList<Dictionary<string, string>> rows)
    {
        var csv = new StringBuilder();

        // Write header row
        csv.AppendLine(string.Join(',', headers.Select(EscapeCsvValue)));

        // Write data rows
        foreach (var row in rows)
        {
            var values = headers.Select(header =>
                row.TryGetValue(header, out var value) ? EscapeCsvValue(value) : string.Empty);

            csv.AppendLine(string.Join(',', values));
        }

        return csv.ToString().TrimEnd();
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Escape if contains comma, quote, or newline
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
