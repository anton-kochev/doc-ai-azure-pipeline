using DocProcessing.Application.Services.Preprocessing;

namespace DocProcessing.Application.Tests.Services.Preprocessing;

public sealed class TextNormalizerTests
{
    private readonly TextNormalizer _sut = new();

    // QUIRK: Inconsistent with TableConverter/FieldParser which throw ArgumentNullException
    [Test]
    public async Task NormalizeText_NullInput_ReturnsEmptyString()
    {
        var result = _sut.NormalizeText(null!);

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task NormalizeText_EmptyString_ReturnsEmptyString()
    {
        var result = _sut.NormalizeText(string.Empty);

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task NormalizeText_WhitespaceOnly_ReturnsEmptyString()
    {
        var result = _sut.NormalizeText("   ");

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task NormalizeText_MultipleSpaces_CollapsedToSingleSpace()
    {
        var result = _sut.NormalizeText("hello   world");

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task NormalizeText_TabsWithinLine_CollapsedToSingleSpace()
    {
        var result = _sut.NormalizeText("hello\t\tworld");

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task NormalizeText_TabOnlyLines_BecomeEmptyAfterTrim()
    {
        var result = _sut.NormalizeText("\t\t");

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task NormalizeText_MixedWhitespaceWithNewlines_LinesTrimmedIndividually()
    {
        var result = _sut.NormalizeText("word1  \n  word2");

        await Assert.That(result).IsEqualTo("word1\nword2");
    }

    [Test]
    public async Task NormalizeText_WindowsNewlines_NormalizedToUnix()
    {
        var result = _sut.NormalizeText("line1\r\nline2");

        await Assert.That(result).IsEqualTo("line1\nline2");
    }

    [Test]
    public async Task NormalizeText_OldMacNewlines_NormalizedToUnix()
    {
        var result = _sut.NormalizeText("line1\rline2");

        await Assert.That(result).IsEqualTo("line1\nline2");
    }

    [Test]
    public async Task NormalizeText_ThreeOrMoreNewlines_CollapsedToDoubleNewline()
    {
        var result = _sut.NormalizeText("para1\n\n\npara2");

        await Assert.That(result).IsEqualTo("para1\n\npara2");
    }

    [Test]
    public async Task NormalizeText_ExactlyTwoNewlines_PreservedAsIs()
    {
        var result = _sut.NormalizeText("para1\n\npara2");

        await Assert.That(result).IsEqualTo("para1\n\npara2");
    }

    [Test]
    public async Task NormalizeText_LeadingTrailingNewlines_NotTrimmedFromOverallResult()
    {
        // Implementation does NOT trim the overall result, only individual lines
        var result = _sut.NormalizeText("\nhello\n");

        await Assert.That(result).IsEqualTo("\nhello\n");
    }

    [Test]
    public async Task NormalizeText_UnicodeNfc_DecomposedToComposed()
    {
        // e + combining acute accent → é
        var result = _sut.NormalizeText("e\u0301");

        await Assert.That(result).IsEqualTo("\u00E9");
    }

    [Test]
    [Arguments("\uFB00", "ff")]
    [Arguments("\uFB01", "fi")]
    [Arguments("\uFB02", "fl")]
    [Arguments("\uFB03", "ffi")]
    [Arguments("\uFB04", "ffl")]
    [Arguments("\uFB05", "ft")]
    [Arguments("\uFB06", "st")]
    public async Task NormalizeText_Ligatures_ExpandedCorrectly(string ligature, string expected)
    {
        var result = _sut.NormalizeText(ligature);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task NormalizeText_CombinedScenario_AppliesAllTransformationsInOrder()
    {
        // Order: newlines → collapse newlines → NFC → ligatures → whitespace per line
        var input = "e\u0301  \uFB01nd\r\n\r\n\r\n  next  ";
        var result = _sut.NormalizeText(input);

        // e\u0301 → é (NFC), \uFB01 → fi (ligature), \r\n→\n, 3 newlines→2, whitespace collapsed
        await Assert.That(result).IsEqualTo("\u00E9 find\n\nnext");
    }

    [Test]
    public async Task NormalizeText_SingleNewlinesBetweenLines_Preserved()
    {
        var result = _sut.NormalizeText("line1\nline2\nline3");

        await Assert.That(result).IsEqualTo("line1\nline2\nline3");
    }

    [Test]
    public async Task NormalizeText_IndividualLinesTrimmed_ButOverallResultNot()
    {
        var result = _sut.NormalizeText("  hello  \n  world  ");

        await Assert.That(result).IsEqualTo("hello\nworld");
    }
}
