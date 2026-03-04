using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Services.Preprocessing;

namespace DocProcessing.Application.Tests.Services.Preprocessing;

public sealed class FieldParserTests
{
    private readonly FieldParser _sut = new();

    private static FormField CreateField(
        string value,
        string? fieldType = null,
        string key = "TestKey",
        double keyConfidence = 0.95,
        double valueConfidence = 0.90,
        int pageNumber = 1) =>
        new(key, value, keyConfidence, valueConfidence, pageNumber, fieldType: fieldType);

    [Test]
    public async Task ParseField_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => _sut.ParseField(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ParseField_NullFieldType_TreatedAsText()
    {
        var field = CreateField("hello", fieldType: null);

        var result = _sut.ParseField(field);

        await Assert.That(result.FieldType).IsEqualTo("text");
        await Assert.That(result.NormalizedValue).IsEqualTo("hello");
        await Assert.That(result.ParsedValue).IsEqualTo("hello");
        await Assert.That(result.OriginalValue).IsEqualTo("hello");
    }

    [Test]
    [Arguments("Date")]
    [Arguments("DATE")]
    [Arguments("date")]
    public async Task ParseField_DateType_CaseInsensitive(string fieldType)
    {
        var field = CreateField("2024-01-15", fieldType: fieldType);

        var result = _sut.ParseField(field);

        await Assert.That(result.FieldType).IsEqualTo("date");
        await Assert.That(result.NormalizedValue).IsEqualTo("2024-01-15");
    }

    [Test]
    public async Task ParseField_UnknownFieldType_TreatedAsText()
    {
        var field = CreateField("123 Main St", fieldType: "address");

        var result = _sut.ParseField(field);

        await Assert.That(result.FieldType).IsEqualTo("text");
        await Assert.That(result.NormalizedValue).IsEqualTo("123 Main St");
        await Assert.That(result.ParsedValue).IsEqualTo("123 Main St");
        await Assert.That(result.OriginalValue).IsEqualTo("123 Main St");
    }

    [Test]
    public async Task ParseField_TextField_PassesThroughUnchanged()
    {
        var field = CreateField("some text", fieldType: "text");

        var result = _sut.ParseField(field);

        await Assert.That(result.FieldType).IsEqualTo("text");
        await Assert.That(result.NormalizedValue).IsEqualTo("some text");
        await Assert.That(result.ParsedValue).IsEqualTo("some text");
        await Assert.That(result.OriginalValue).IsEqualTo("some text");
    }

    public sealed class DateParsingTests
    {
        private readonly FieldParser _sut = new();

        [Test]
        [Arguments("01/15/2024", "2024-01-15")]
        [Arguments("2024-01-15", "2024-01-15")]
        [Arguments("Jan 15, 2024", "2024-01-15")]
        [Arguments("15 January 2024", "2024-01-15")]
        public async Task ParseField_DateFormats_NormalizedToIso(string input, string expected)
        {
            var field = CreateField(input, fieldType: "date");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("date");
            await Assert.That(result.NormalizedValue).IsEqualTo(expected);
            await Assert.That(result.OriginalValue).IsEqualTo(input);
        }

        [Test]
        public async Task ParseField_DateParsed_ParsedValueIsDateTime()
        {
            var field = CreateField("2024-01-15", fieldType: "date");

            var result = _sut.ParseField(field);

            await Assert.That(result.ParsedValue).IsTypeOf<DateTime>();
            var dateValue = (DateTime)result.ParsedValue!;
            await Assert.That(dateValue.Year).IsEqualTo(2024);
            await Assert.That(dateValue.Month).IsEqualTo(1);
            await Assert.That(dateValue.Day).IsEqualTo(15);
        }

        // QUIRK: Ambiguous date 01/02/2024 parses as January 2nd (MM/dd tried before dd/MM)
        [Test]
        public async Task ParseField_AmbiguousDate_USFormatPriority()
        {
            var field = CreateField("01/02/2024", fieldType: "date");

            var result = _sut.ParseField(field);

            await Assert.That(result.NormalizedValue).IsEqualTo("2024-01-02"); // January 2nd, not February 1st
        }

        [Test]
        public async Task ParseField_DateEmpty_FallsBackToText()
        {
            var field = CreateField("", fieldType: "date");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("text");
            await Assert.That(result.NormalizedValue).IsEqualTo(string.Empty);
            await Assert.That(result.ParsedValue).IsNull();
        }

        [Test]
        public async Task ParseField_DateWhitespace_FallsBackToText()
        {
            var field = CreateField("   ", fieldType: "date");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("text");
        }

        [Test]
        public async Task ParseField_DateUnparseable_FallsBackToText()
        {
            var field = CreateField("not a date", fieldType: "date");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("text");
            await Assert.That(result.NormalizedValue).IsEqualTo("not a date");
            await Assert.That(result.ParsedValue).IsNull();
            await Assert.That(result.OriginalValue).IsEqualTo("not a date");
        }
    }

    public sealed class CurrencyParsingTests
    {
        private readonly FieldParser _sut = new();

        [Test]
        public async Task ParseField_CurrencyDollar_ParsedCorrectly()
        {
            var field = CreateField("$1,234.56", fieldType: "currency");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("currency");
            await Assert.That(result.NormalizedValue).IsEqualTo("1234.56");
            await Assert.That(result.ParsedValue).IsTypeOf<decimal>();
            await Assert.That((decimal)result.ParsedValue!).IsEqualTo(1234.56m);
            await Assert.That(result.OriginalValue).IsEqualTo("$1,234.56");
        }

        [Test]
        public async Task ParseField_CurrencyEuro_ParsedCorrectly()
        {
            var field = CreateField("€100", fieldType: "currency");

            var result = _sut.ParseField(field);

            await Assert.That(result.NormalizedValue).IsEqualTo("100.00");
            await Assert.That(result.ParsedValue).IsTypeOf<decimal>();
        }

        [Test]
        public async Task ParseField_CurrencyNegative_ParsedCorrectly()
        {
            var field = CreateField("-$500", fieldType: "currency");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("currency");
            await Assert.That(result.NormalizedValue).IsEqualTo("-500.00");
            await Assert.That((decimal)result.ParsedValue!).IsEqualTo(-500m);
        }

        // QUIRK: Only $, €, £, ¥ symbols are stripped. Other currency symbols
        // (CHF, Rs., etc.) remain and cause parsing to fall back to text.
        [Test]
        [Arguments("CHF 100")]
        [Arguments("Rs. 500")]
        public async Task ParseField_CurrencyUnsupportedSymbol_FallsBackToText(string input)
        {
            var field = CreateField(input, fieldType: "currency");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("text");
            await Assert.That(result.NormalizedValue).IsEqualTo(input);
            await Assert.That(result.ParsedValue).IsNull();
            await Assert.That(result.OriginalValue).IsEqualTo(input);
        }

        [Test]
        public async Task ParseField_CurrencyEmpty_FallsBackToText()
        {
            var field = CreateField("", fieldType: "currency");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("text");
            await Assert.That(result.NormalizedValue).IsEqualTo(string.Empty);
            await Assert.That(result.ParsedValue).IsNull();
        }
    }

    public sealed class NumberParsingTests
    {
        private readonly FieldParser _sut = new();

        [Test]
        public async Task ParseField_NumberInteger_ParsedAsInt()
        {
            var field = CreateField("1,234", fieldType: "number");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("number");
            await Assert.That(result.NormalizedValue).IsEqualTo("1234");
            await Assert.That(result.ParsedValue).IsTypeOf<int>();
            await Assert.That((int)result.ParsedValue!).IsEqualTo(1234);
            await Assert.That(result.OriginalValue).IsEqualTo("1,234");
        }

        [Test]
        public async Task ParseField_NumberDecimal_ParsedAsDecimal()
        {
            var field = CreateField("3.14", fieldType: "number");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("number");
            await Assert.That(result.NormalizedValue).IsEqualTo("3.14");
            await Assert.That(result.ParsedValue).IsTypeOf<decimal>();
            await Assert.That((decimal)result.ParsedValue!).IsEqualTo(3.14m);
        }

        [Test]
        public async Task ParseField_NumberExceedsIntMaxValue_FallsThroughToDecimal()
        {
            // int.MaxValue = 2,147,483,647 — 3 billion exceeds it
            var field = CreateField("3000000000", fieldType: "number");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("number");
            await Assert.That(result.ParsedValue).IsTypeOf<decimal>();
            await Assert.That((decimal)result.ParsedValue!).IsEqualTo(3000000000m);
        }

        [Test]
        public async Task ParseField_NumberWithLeadingTrailingSpaces_ParsedCorrectly()
        {
            var field = CreateField(" 1,234 ", fieldType: "number");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("number");
            await Assert.That(result.NormalizedValue).IsEqualTo("1234");
        }

        [Test]
        public async Task ParseField_NumberUnparseable_FallsBackToText()
        {
            var field = CreateField("abc", fieldType: "number");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("text");
            await Assert.That(result.NormalizedValue).IsEqualTo("abc");
            await Assert.That(result.ParsedValue).IsNull();
            await Assert.That(result.OriginalValue).IsEqualTo("abc");
        }

        [Test]
        public async Task ParseField_NumberEmpty_FallsBackToText()
        {
            var field = CreateField("", fieldType: "number");

            var result = _sut.ParseField(field);

            await Assert.That(result.FieldType).IsEqualTo("text");
        }
    }

    public sealed class PreservedFieldTests
    {
        private readonly FieldParser _sut = new();

        [Test]
        public async Task ParseField_ConfidenceAndPageNumber_PreservedFromSource()
        {
            var field = CreateField("hello", fieldType: "text",
                keyConfidence: 0.88, valueConfidence: 0.76, pageNumber: 3);

            var result = _sut.ParseField(field);

            await Assert.That(result.KeyConfidence).IsEqualTo(0.88);
            await Assert.That(result.ValueConfidence).IsEqualTo(0.76);
            await Assert.That(result.PageNumber).IsEqualTo(3);
        }
    }
}
