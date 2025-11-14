namespace DocProcessing.Application.Services.Preprocessing;

/// <summary>
/// Provides text normalization services for preprocessing.
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Normalizes text by applying whitespace cleanup, Unicode normalization (NFC),
    /// ligature expansion, and newline standardization.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    string NormalizeText(string text);
}
