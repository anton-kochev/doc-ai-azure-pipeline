using System.Globalization;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Models.Preprocessing;

namespace DocProcessing.Application.Services.Preprocessing;

/// <summary>
/// Provides form field parsing services for preprocessing.
/// </summary>
public sealed class FieldParser : IFieldParser
{
    /// <inheritdoc/>
    public NormalizedFormField ParseField(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var (normalizedValue, parsedValue, fieldType) = field.FieldType?.ToLowerInvariant() switch
        {
            "date" => ParseDate(field.Value),
            "currency" => ParseCurrency(field.Value),
            "number" => ParseNumber(field.Value),
            _ => (field.Value, field.Value, "text")
        };

        return new NormalizedFormField
        {
            Key = field.Key,
            OriginalValue = field.Value,
            NormalizedValue = normalizedValue,
            FieldType = fieldType,
            ParsedValue = parsedValue,
            KeyConfidence = field.KeyConfidence,
            ValueConfidence = field.ValueConfidence,
            PageNumber = field.PageNumber
        };
    }

    private static (string NormalizedValue, object? ParsedValue, string FieldType) ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (string.Empty, null, "text");
        }

        // Try various date formats
        string[] dateFormats =
        [
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy",
            "M/d/yyyy",
            "d/M/yyyy",
            "yyyy/MM/dd",
            "MMM d, yyyy",
            "MMMM d, yyyy",
            "d MMM yyyy",
            "d MMMM yyyy"
        ];

        foreach (var format in dateFormats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return (date.ToString("yyyy-MM-dd"), date, "date");
            }
        }

        // Fallback: try general parsing
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return (parsedDate.ToString("yyyy-MM-dd"), parsedDate, "date");
        }

        // Failed to parse as date, treat as text
        return (value, null, "text");
    }

    private static (string NormalizedValue, object? ParsedValue, string FieldType) ParseCurrency(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (string.Empty, null, "text");
        }

        // Remove currency symbols and whitespace
        var cleaned = value
            .Replace("$", "")
            .Replace("€", "")
            .Replace("£", "")
            .Replace("¥", "")
            .Replace(" ", "")
            .Trim();

        // Try parsing as decimal
        if (decimal.TryParse(cleaned, NumberStyles.Currency, CultureInfo.InvariantCulture, out var amount))
        {
            return (amount.ToString("F2", CultureInfo.InvariantCulture), amount, "currency");
        }

        // Fallback: treat as text
        return (value, null, "text");
    }

    private static (string NormalizedValue, object? ParsedValue, string FieldType) ParseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (string.Empty, null, "text");
        }

        // Remove thousand separators
        var cleaned = value.Replace(",", "").Replace(" ", "").Trim();

        // Try parsing as integer first
        if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return (intValue.ToString(CultureInfo.InvariantCulture), intValue, "number");
        }

        // Try parsing as decimal
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return (decimalValue.ToString(CultureInfo.InvariantCulture), decimalValue, "number");
        }

        // Fallback: treat as text
        return (value, null, "text");
    }
}
