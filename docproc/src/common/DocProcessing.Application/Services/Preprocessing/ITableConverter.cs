using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Models.Preprocessing;

namespace DocProcessing.Application.Services.Preprocessing;

/// <summary>
/// Provides table conversion services for preprocessing.
/// </summary>
public interface ITableConverter
{
    /// <summary>
    /// Converts an OCR table to structured format (JSON/CSV) with headers and rows.
    /// </summary>
    /// <param name="table">The OCR table data to convert.</param>
    /// <returns>A structured table with headers, rows, and JSON/CSV representations.</returns>
    StructuredTable ConvertToStructured(TableData table);
}
