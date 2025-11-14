using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Models.Preprocessing;

namespace DocProcessing.Application.Services.Preprocessing;

/// <summary>
/// Provides form field parsing services for preprocessing.
/// </summary>
public interface IFieldParser
{
    /// <summary>
    /// Parses and normalizes a form field, converting dates, currencies, and numbers to typed values.
    /// </summary>
    /// <param name="field">The OCR form field to parse.</param>
    /// <returns>A normalized form field with parsed typed value.</returns>
    NormalizedFormField ParseField(FormField field);
}
