namespace DocProcessing.Application.Models.OCR;

/// <summary>
/// Represents OCR results for a single page.
/// </summary>
public sealed class OcrPage
{
    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Page width in points (1/72 inch).
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// Page height in points (1/72 inch).
    /// </summary>
    public double Height { get; init; }

    /// <summary>
    /// Text blocks extracted from this page.
    /// </summary>
    public IReadOnlyList<TextBlock> TextBlocks { get; init; } = [];

    /// <summary>
    /// Tables extracted from this page.
    /// </summary>
    public IReadOnlyList<TableData> Tables { get; init; } = [];

    /// <summary>
    /// Form fields (key-value pairs) extracted from this page.
    /// </summary>
    public IReadOnlyList<FormField> FormFields { get; init; } = [];

    /// <summary>
    /// Detected language for this page.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Text orientation angle in degrees (0, 90, 180, 270).
    /// </summary>
    public double Angle { get; init; }

    /// <summary>
    /// Overall confidence score for this page (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    public OcrPage(
        int pageNumber,
        double width,
        double height,
        double confidence,
        IReadOnlyList<TextBlock>? textBlocks = null,
        IReadOnlyList<TableData>? tables = null,
        IReadOnlyList<FormField>? formFields = null,
        string? language = null,
        double angle = 0)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Must be >= 1");
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Must be > 0");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Must be > 0");
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Must be between 0 and 1");

        PageNumber = pageNumber;
        Width = width;
        Height = height;
        Confidence = confidence;
        TextBlocks = textBlocks ?? [];
        Tables = tables ?? [];
        FormFields = formFields ?? [];
        Language = language;
        Angle = angle;
    }
}
