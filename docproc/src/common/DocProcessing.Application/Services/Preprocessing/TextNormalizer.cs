using System.Text;
using System.Text.RegularExpressions;

namespace DocProcessing.Application.Services.Preprocessing;

/// <summary>
/// Provides text normalization services for preprocessing.
/// </summary>
public sealed partial class TextNormalizer : ITextNormalizer
{
    // Common ligatures mapping
    private static readonly Dictionary<string, string> LigatureMap = new()
    {
        ["\uFB00"] = "ff",  // ﬀ
        ["\uFB01"] = "fi",  // ﬁ
        ["\uFB02"] = "fl",  // ﬂ
        ["\uFB03"] = "ffi", // ﬃ
        ["\uFB04"] = "ffl", // ﬄ
        ["\uFB05"] = "ft",  // ﬅ
        ["\uFB06"] = "st",  // ﬆ
    };

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultipleNewlinesRegex();

    /// <inheritdoc/>
    public string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // 1. Normalize newlines (convert \r\n and \r to \n)
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // 2. Collapse multiple newlines to double newline (preserve paragraph breaks)
        text = MultipleNewlinesRegex().Replace(text, "\n\n");

        // 3. Unicode normalization (to NFC - composed form)
        text = text.Normalize(NormalizationForm.FormC);

        // 4. Expand ligatures
        foreach (var (ligature, expansion) in LigatureMap)
        {
            text = text.Replace(ligature, expansion);
        }

        // 5. Normalize whitespace (collapse multiple spaces to single space, but preserve line breaks)
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = MultipleWhitespaceRegex().Replace(lines[i], " ").Trim();
        }
        text = string.Join('\n', lines);

        return text;
    }
}
