namespace DocProcessing.Application.Models.OCR;

/// <summary>
/// Represents a key-value pair extracted from a form document.
/// Named FormField to avoid conflict with System.Collections.Generic.KeyValuePair.
/// </summary>
public sealed class FormField
{
    /// <summary>
    /// Field name/key (e.g., "Invoice Number", "Date", "Total Amount").
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Field value (e.g., "INV-12345", "2024-01-15", "$1,234.56").
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Confidence score for the key extraction (0.0 to 1.0).
    /// </summary>
    public double KeyConfidence { get; init; }

    /// <summary>
    /// Confidence score for the value extraction (0.0 to 1.0).
    /// </summary>
    public double ValueConfidence { get; init; }

    /// <summary>
    /// Bounding box for the key.
    /// </summary>
    public BoundingBox? KeyBoundingBox { get; init; }

    /// <summary>
    /// Bounding box for the value.
    /// </summary>
    public BoundingBox? ValueBoundingBox { get; init; }

    /// <summary>
    /// Page number where this field appears (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Field type hint (e.g., "text", "date", "number", "currency").
    /// </summary>
    public string? FieldType { get; init; }

    public FormField(
        string key,
        string value,
        double keyConfidence,
        double valueConfidence,
        int pageNumber,
        BoundingBox? keyBoundingBox = null,
        BoundingBox? valueBoundingBox = null,
        string? fieldType = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cannot be null or whitespace", nameof(key));
        if (keyConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(keyConfidence), "Must be between 0 and 1");
        if (valueConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(valueConfidence), "Must be between 0 and 1");
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Must be >= 1");

        Key = key;
        Value = value;
        KeyConfidence = keyConfidence;
        ValueConfidence = valueConfidence;
        PageNumber = pageNumber;
        KeyBoundingBox = keyBoundingBox;
        ValueBoundingBox = valueBoundingBox;
        FieldType = fieldType;
    }
}
