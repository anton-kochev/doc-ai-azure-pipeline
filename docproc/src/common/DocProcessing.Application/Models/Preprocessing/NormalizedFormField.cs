namespace DocProcessing.Application.Models.Preprocessing;

/// <summary>
/// Represents a normalized form field with parsed typed values.
/// </summary>
public sealed class NormalizedFormField
{
    /// <summary>
    /// Gets or sets the field key/name.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the original value from OCR.
    /// </summary>
    public string OriginalValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the normalized value (cleaned, standardized format).
    /// </summary>
    public string NormalizedValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the detected field type from OCR or inferred (text, date, number, currency).
    /// </summary>
    public string FieldType { get; init; } = "text";

    /// <summary>
    /// Gets or sets the parsed typed value (for dates, numbers, currencies).
    /// </summary>
    public object? ParsedValue { get; init; }

    /// <summary>
    /// Gets or sets the confidence score for the key from OCR.
    /// </summary>
    public double KeyConfidence { get; init; }

    /// <summary>
    /// Gets or sets the confidence score for the value from OCR.
    /// </summary>
    public double ValueConfidence { get; init; }

    /// <summary>
    /// Gets or sets the page number where this field appears.
    /// </summary>
    public int PageNumber { get; init; }
}
