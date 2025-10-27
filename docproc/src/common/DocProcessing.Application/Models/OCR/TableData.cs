namespace DocProcessing.Application.Models.OCR;

/// <summary>
/// Represents a structured table extracted from a document.
/// </summary>
public sealed class TableData
{
    /// <summary>
    /// Number of rows in the table.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// Number of columns in the table.
    /// </summary>
    public int ColumnCount { get; init; }

    /// <summary>
    /// Table cells organized as a list (row-major order).
    /// </summary>
    public IReadOnlyList<TableCell> Cells { get; init; } = [];

    /// <summary>
    /// Bounding box coordinates for the entire table.
    /// </summary>
    public BoundingBox? BoundingBox { get; init; }

    /// <summary>
    /// Page number where this table appears (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Confidence score for the table extraction (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    public TableData(
        int rowCount,
        int columnCount,
        IReadOnlyList<TableCell> cells,
        int pageNumber,
        double confidence,
        BoundingBox? boundingBox = null)
    {
        if (rowCount < 1) throw new ArgumentOutOfRangeException(nameof(rowCount), "Must be >= 1");
        if (columnCount < 1) throw new ArgumentOutOfRangeException(nameof(columnCount), "Must be >= 1");
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber), "Must be >= 1");
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Must be between 0 and 1");

        RowCount = rowCount;
        ColumnCount = columnCount;
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        PageNumber = pageNumber;
        Confidence = confidence;
        BoundingBox = boundingBox;
    }
}

/// <summary>
/// Represents a single cell in a table.
/// </summary>
public sealed class TableCell
{
    /// <summary>
    /// Row index (0-based).
    /// </summary>
    public int RowIndex { get; init; }

    /// <summary>
    /// Column index (0-based).
    /// </summary>
    public int ColumnIndex { get; init; }

    /// <summary>
    /// Cell content (text).
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Number of rows this cell spans.
    /// </summary>
    public int RowSpan { get; init; } = 1;

    /// <summary>
    /// Number of columns this cell spans.
    /// </summary>
    public int ColumnSpan { get; init; } = 1;

    /// <summary>
    /// Whether this cell is a header cell.
    /// </summary>
    public bool IsHeader { get; init; }

    /// <summary>
    /// Confidence score for this cell (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Bounding box for this cell.
    /// </summary>
    public BoundingBox? BoundingBox { get; init; }

    public TableCell(
        int rowIndex,
        int columnIndex,
        string content,
        double confidence,
        bool isHeader = false,
        int rowSpan = 1,
        int columnSpan = 1,
        BoundingBox? boundingBox = null)
    {
        if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex), "Must be >= 0");
        if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex), "Must be >= 0");
        if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan), "Must be >= 1");
        if (columnSpan < 1) throw new ArgumentOutOfRangeException(nameof(columnSpan), "Must be >= 1");
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Must be between 0 and 1");

        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Content = content;
        Confidence = confidence;
        IsHeader = isHeader;
        RowSpan = rowSpan;
        ColumnSpan = columnSpan;
        BoundingBox = boundingBox;
    }
}
