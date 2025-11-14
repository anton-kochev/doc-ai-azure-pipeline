using DocProcessing.Application.Models.OCR;

namespace DocProcessing.Application.Models.Preprocessing;

/// <summary>
/// Represents a table converted to structured format (JSON/CSV).
/// </summary>
public sealed class StructuredTable
{
    /// <summary>
    /// Gets or sets the table number (1-based index).
    /// </summary>
    public int TableNumber { get; init; }

    /// <summary>
    /// Gets or sets the page number where this table appears.
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Gets or sets the headers extracted from the table.
    /// </summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>
    /// Gets or sets the rows as dictionaries (header -> cell value).
    /// </summary>
    public IReadOnlyList<Dictionary<string, string>> Rows { get; init; } = [];

    /// <summary>
    /// Gets or sets the JSON representation of the table.
    /// </summary>
    public string JsonRepresentation { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the CSV representation of the table.
    /// </summary>
    public string CsvRepresentation { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the original table confidence from OCR.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets or sets the bounding box from OCR.
    /// </summary>
    public BoundingBox? BoundingBox { get; init; }
}
