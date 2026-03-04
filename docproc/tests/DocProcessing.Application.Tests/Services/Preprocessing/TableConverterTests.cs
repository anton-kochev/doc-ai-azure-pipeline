using System.Text.Json;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Models.Preprocessing;
using DocProcessing.Application.Services.Preprocessing;

namespace DocProcessing.Application.Tests.Services.Preprocessing;

public sealed class TableConverterTests
{
    private readonly TableConverter _sut = new();

    private static TableData CreateTable(
        int rowCount,
        int columnCount,
        IReadOnlyList<TableCell> cells,
        int pageNumber = 1,
        double confidence = 0.95,
        BoundingBox? boundingBox = null) =>
        new(rowCount, columnCount, cells, pageNumber, confidence, boundingBox);

    private static TableCell CreateCell(
        int rowIndex,
        int columnIndex,
        string content,
        bool isHeader = false,
        int columnSpan = 1,
        double confidence = 0.9) =>
        new(rowIndex, columnIndex, content, confidence, isHeader, columnSpan: columnSpan);

    [Test]
    public async Task ConvertToStructured_NullInput_ThrowsArgumentNullException()
    {
        await Assert.That(() => _sut.ConvertToStructured(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    public sealed class HeaderExtractionTests
    {
        private readonly TableConverter _sut = new();

        [Test]
        public async Task ConvertToStructured_CellsWithIsHeader_UsesHeaderCells()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Name", isHeader: true),
                CreateCell(0, 1, "Age", isHeader: true),
                CreateCell(1, 0, "Alice"),
                CreateCell(1, 1, "30"),
            };
            var table = CreateTable(2, 2, cells);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Headers).IsEquivalentTo(new[] { "Name", "Age" });
            await Assert.That(result.Rows).Count().IsEqualTo(1);
            await Assert.That(result.Rows[0]["Name"]).IsEqualTo("Alice");
            await Assert.That(result.Rows[0]["Age"]).IsEqualTo("30");
        }

        [Test]
        public async Task ConvertToStructured_NoHeaderCells_FirstRowUsedAsHeaders()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Name"),
                CreateCell(0, 1, "Age"),
                CreateCell(1, 0, "Alice"),
                CreateCell(1, 1, "30"),
            };
            var table = CreateTable(2, 2, cells);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Headers).IsEquivalentTo(new[] { "Name", "Age" });
            await Assert.That(result.Rows).Count().IsEqualTo(1);
            await Assert.That(result.Rows[0]["Name"]).IsEqualTo("Alice");
        }

        [Test]
        public async Task ConvertToStructured_NoCells_GeneratesColumnNames()
        {
            var table = CreateTable(1, 3, []);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Headers).IsEquivalentTo(new[] { "Column1", "Column2", "Column3" });
        }

        // QUIRK: Header extraction ignores ColumnSpan on header cells.
        // Only Content is used; the span doesn't affect header count.
        [Test]
        public async Task ConvertToStructured_HeaderWithColumnSpan_SpanIgnoredInHeaders()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Merged", isHeader: true, columnSpan: 2),
                CreateCell(1, 0, "A"),
                CreateCell(1, 1, "B"),
            };
            var table = CreateTable(2, 2, cells);

            var result = _sut.ConvertToStructured(table);

            // Only one header despite 2-column table
            await Assert.That(result.Headers).Count().IsEqualTo(1);
            await Assert.That(result.Headers[0]).IsEqualTo("Merged");
        }

        // QUIRK: Duplicate header names cause silent overwrite in row dictionaries
        [Test]
        public async Task ConvertToStructured_DuplicateHeaders_LastValueWins()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Col", isHeader: true),
                CreateCell(0, 1, "Col", isHeader: true),
                CreateCell(1, 0, "First"),
                CreateCell(1, 1, "Second"),
            };
            var table = CreateTable(2, 2, cells);

            var result = _sut.ConvertToStructured(table);

            // Dictionary overwrites: second cell with same header key wins
            await Assert.That(result.Rows[0]["Col"]).IsEqualTo("Second");
        }

        [Test]
        public async Task ConvertToStructured_HasHeaderAndNonHeaderOnRow0_SkipsRow0AsData()
        {
            // Mixed: header cell at (0,0), non-header cell at (0,1)
            var cells = new[]
            {
                CreateCell(0, 0, "H1", isHeader: true),
                CreateCell(0, 1, "NotHeader"),
                CreateCell(1, 0, "V1"),
                CreateCell(1, 1, "V2"),
            };
            var table = CreateTable(2, 2, cells);

            var result = _sut.ConvertToStructured(table);

            // Header extracted from IsHeader cells only: ["H1"]
            await Assert.That(result.Headers).Count().IsEqualTo(1);
            // hasHeaderRow is true (IsHeader cells on row 0), so start from row 1
            await Assert.That(result.Rows).Count().IsEqualTo(1);
        }
    }

    public sealed class RowBuildingTests
    {
        private readonly TableConverter _sut = new();

        // BUG: Column span on data cells — loop variable `i` is not advanced after span,
        // so the next cell in cellsInRow overwrites spanned values.
        [Test]
        public async Task ConvertToStructured_DataCellColumnSpan_LoopNotAdvanced()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "A", isHeader: true),
                CreateCell(0, 1, "B", isHeader: true),
                CreateCell(0, 2, "C", isHeader: true),
                // Data row: first cell spans 2 columns, then a normal cell
                CreateCell(1, 0, "Span2", columnSpan: 2),
                CreateCell(1, 2, "Normal"),
            };
            var table = CreateTable(2, 3, cells);

            var result = _sut.ConvertToStructured(table);

            // BUG: After spanning "Span2" into A and B, i=1 picks up "Normal" cell
            // and assigns it to headers[1] = "B", overwriting "Span2"
            await Assert.That(result.Rows[0]["A"]).IsEqualTo("Span2");
            await Assert.That(result.Rows[0]["B"]).IsEqualTo("Normal"); // BUG: should be "Span2"
            await Assert.That(result.Rows[0].ContainsKey("C")).IsFalse(); // "Normal" not in C
        }

        [Test]
        public async Task ConvertToStructured_FewerCellsThanHeaders_MissingKeysOmitted()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "A", isHeader: true),
                CreateCell(0, 1, "B", isHeader: true),
                CreateCell(0, 2, "C", isHeader: true),
                CreateCell(1, 0, "V1"),
            };
            var table = CreateTable(2, 3, cells);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Rows[0].ContainsKey("A")).IsTrue();
            await Assert.That(result.Rows[0].ContainsKey("B")).IsFalse();
            await Assert.That(result.Rows[0].ContainsKey("C")).IsFalse();
        }

        [Test]
        public async Task ConvertToStructured_MoreCellsThanHeaders_ExtraCellsTruncated()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "A", isHeader: true),
                CreateCell(1, 0, "V1"),
                CreateCell(1, 1, "V2"), // extra — no matching header
            };
            var table = CreateTable(2, 2, cells);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Rows[0].Count).IsEqualTo(1);
            await Assert.That(result.Rows[0]["A"]).IsEqualTo("V1");
        }

        [Test]
        public async Task ConvertToStructured_EmptyCellContent_StillAppearsInRow()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "A", isHeader: true),
                CreateCell(1, 0, ""),
            };
            var table = CreateTable(2, 1, cells);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Rows[0]["A"]).IsEqualTo(string.Empty);
        }

        [Test]
        public async Task ConvertToStructured_EmptyTable_NoCells_NoRows()
        {
            var table = CreateTable(1, 1, []);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Rows).Count().IsEqualTo(0);
        }
    }

    public sealed class OutputTests
    {
        private readonly TableConverter _sut = new();

        [Test]
        public async Task ConvertToStructured_JsonRepresentation_DeserializesToCorrectStructure()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Name", isHeader: true),
                CreateCell(0, 1, "Age", isHeader: true),
                CreateCell(1, 0, "Alice"),
                CreateCell(1, 1, "30"),
            };
            var table = CreateTable(2, 2, cells);

            var result = _sut.ConvertToStructured(table);

            var deserialized = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(
                result.JsonRepresentation);
            await Assert.That(deserialized).IsNotNull();
            await Assert.That(deserialized!).Count().IsEqualTo(1);
            await Assert.That(deserialized![0]["Name"]).IsEqualTo("Alice");
            await Assert.That(deserialized![0]["Age"]).IsEqualTo("30");
        }

        [Test]
        public async Task ConvertToStructured_CsvWithCommaInValue_ProperlyEscaped()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Item", isHeader: true),
                CreateCell(1, 0, "A, B"),
            };
            var table = CreateTable(2, 1, cells);

            var result = _sut.ConvertToStructured(table);

            var lines = result.CsvRepresentation.Split(Environment.NewLine);
            await Assert.That(lines[0]).IsEqualTo("Item");
            await Assert.That(lines[1]).IsEqualTo("\"A, B\"");
        }

        [Test]
        public async Task ConvertToStructured_CsvWithQuotesInValue_DoubleEscaped()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Item", isHeader: true),
                CreateCell(1, 0, "Say \"hi\""),
            };
            var table = CreateTable(2, 1, cells);

            var result = _sut.ConvertToStructured(table);

            var lines = result.CsvRepresentation.Split(Environment.NewLine);
            await Assert.That(lines[1]).IsEqualTo("\"Say \"\"hi\"\"\"");
        }

        [Test]
        public async Task ConvertToStructured_CsvWithNewlineInValue_Escaped()
        {
            var cells = new[]
            {
                CreateCell(0, 0, "Item", isHeader: true),
                CreateCell(1, 0, "Line1\nLine2"),
            };
            var table = CreateTable(2, 1, cells);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.CsvRepresentation).Contains("\"Line1\nLine2\"");
        }

        [Test]
        public async Task ConvertToStructured_TableNumber_HardcodedToOne()
        {
            // TableNumber is always 1 — caller is expected to override
            var table = CreateTable(1, 1, []);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.TableNumber).IsEqualTo(1);
        }

        [Test]
        public async Task ConvertToStructured_BoundingBoxAndConfidence_PreservedFromSource()
        {
            var bbox = new BoundingBox(0.1, 0.2, 0.3, 0.4);
            var table = CreateTable(1, 1, [], confidence: 0.88, boundingBox: bbox);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.Confidence).IsEqualTo(0.88);
            await Assert.That(result.BoundingBox).IsNotNull();
            await Assert.That(result.BoundingBox!.X).IsEqualTo(0.1);
        }

        [Test]
        public async Task ConvertToStructured_PageNumber_PreservedFromSource()
        {
            var table = CreateTable(1, 1, [], pageNumber: 5);

            var result = _sut.ConvertToStructured(table);

            await Assert.That(result.PageNumber).IsEqualTo(5);
        }
    }
}
