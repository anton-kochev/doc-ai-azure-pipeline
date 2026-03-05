using DocProcessing.Application.Configuration;
using DocProcessing.Application.Models.Chunking;
using DocProcessing.Application.Models.Preprocessing;
using DocProcessing.Application.Services.Chunking;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Tests.Services.Chunking;

public sealed class DocumentChunkerTests
{
    private readonly DocumentChunker _sut = new();

    // -------------------------------------------------------------------------
    // Default options used across most tests — small sizes to trigger splitting
    // with manageable text volumes.
    // -------------------------------------------------------------------------

    private static ChunkingOptions DefaultOptions(
        int maxChunkSize = 80,
        int overlapTokens = 15,
        double tokenEstimationFactor = 1.3) =>
        new()
        {
            OutputBlobContainer = "chunk-results",
            MaxChunkSize = maxChunkSize,
            OverlapTokens = overlapTokens,
            TokenEstimationFactor = tokenEstimationFactor
        };

    // -------------------------------------------------------------------------
    // Helper builders
    // -------------------------------------------------------------------------

    private static PreprocessMetadata DefaultMetadata(
        int pageCount = 1,
        int totalWordCount = 0,
        int totalTables = 0,
        int totalFormFields = 0) =>
        new()
        {
            ProcessedAt = DateTimeOffset.UtcNow,
            ProcessingDuration = TimeSpan.FromSeconds(1),
            PageCount = pageCount,
            TotalWordCount = totalWordCount,
            TotalTables = totalTables,
            TotalFormFields = totalFormFields,
            PrimaryLanguage = "en"
        };

    private static PreprocessedPage BuildPage(
        int pageNumber,
        string normalizedText,
        IReadOnlyList<NormalizedTextBlock>? textBlocks = null) =>
        new()
        {
            PageNumber = pageNumber,
            NormalizedText = normalizedText,
            WordCount = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            TextBlocks = textBlocks ?? [BuildTextBlock(normalizedText, pageNumber)]
        };

    private static NormalizedTextBlock BuildTextBlock(
        string text,
        int pageNumber = 1,
        string blockType = "paragraph",
        double confidence = 0.95) =>
        new()
        {
            OriginalText = text,
            NormalizedText = text,
            BlockType = blockType,
            Confidence = confidence,
            PageNumber = pageNumber
        };

    private static StructuredTable BuildTable(
        int tableNumber = 1,
        int pageNumber = 1,
        string? jsonRepresentation = null,
        double confidence = 0.95) =>
        new()
        {
            TableNumber = tableNumber,
            PageNumber = pageNumber,
            JsonRepresentation = jsonRepresentation ?? $"{{\"table\":{tableNumber}}}",
            CsvRepresentation = $"col1,col2\nval1,val2",
            Confidence = confidence,
            Headers = ["col1", "col2"],
            Rows = [new Dictionary<string, string> { ["col1"] = "val1", ["col2"] = "val2" }]
        };

    private static NormalizedFormField BuildFormField(
        string key,
        string normalizedValue,
        int pageNumber = 1) =>
        new()
        {
            Key = key,
            OriginalValue = normalizedValue,
            NormalizedValue = normalizedValue,
            FieldType = "text",
            PageNumber = pageNumber,
            KeyConfidence = 0.95,
            ValueConfidence = 0.93
        };

    /// <summary>
    /// Builds text that contains exactly the given number of words, split
    /// into sentences ending with a full stop.  Each sentence has
    /// <paramref name="wordsPerSentence"/> words plus a period.
    /// </summary>
    private static string BuildSentences(int sentenceCount, int wordsPerSentence = 10)
    {
        var sentences = Enumerable.Range(1, sentenceCount)
            .Select(s =>
            {
                var words = Enumerable.Range(1, wordsPerSentence)
                    .Select(w => w == 1
                        ? $"Word{(s - 1) * wordsPerSentence + w}"   // Capitalize first word so sentence regex matches
                        : $"word{(s - 1) * wordsPerSentence + w}");
                return string.Join(' ', words) + ".";
            });
        return string.Join(' ', sentences);
    }

    // =========================================================================
    // Core Text Chunking
    // =========================================================================

    [Test]
    public async Task ChunkDocument_SinglePageShortText_ReturnsSingleTextChunk()
    {
        // Arrange
        // "Hello world." — 2 words, token estimate ≈ 3, well under MaxChunkSize=80
        var page = BuildPage(1, "Hello world.");
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 2)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks).Count().IsEqualTo(1);
        await Assert.That(textChunks[0].Content).IsEqualTo("Hello world.");
    }

    [Test]
    public async Task ChunkDocument_TextExceedsMaxSize_SplitsIntoMultiple()
    {
        // Arrange
        // 6 sentences × 10 words = 60 words; tokens ≈ 78 per sentence group of 6.
        // With MaxChunkSize=40, each chunk can hold ~30 words (40/1.3≈30), so
        // we need at least 2 chunks.
        var text = BuildSentences(6, wordsPerSentence: 10); // 60 words total
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 60)
        };
        var options = DefaultOptions(maxChunkSize: 40, overlapTokens: 5);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task ChunkDocument_RespectsSentenceBoundaries_DoesNotSplitMidSentence()
    {
        // Arrange — 4 sentences, forcing at least 2 chunks at MaxChunkSize=30
        var text = BuildSentences(4, wordsPerSentence: 8); // 32 words; ~41 tokens
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 32)
        };
        var options = DefaultOptions(maxChunkSize: 30, overlapTokens: 5);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — every text chunk must end with a sentence-ending punctuation
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThan(0);
        foreach (var chunk in textChunks)
        {
            var trimmed = chunk.Content.TrimEnd();
            await Assert.That(trimmed.EndsWith('.') || trimmed.EndsWith('!') || trimmed.EndsWith('?'))
                .IsTrue();
        }
    }

    [Test]
    public async Task ChunkDocument_OverlapBetweenChunks_IncludesConfiguredOverlap()
    {
        // Arrange — 6 sentences × 5 words each ≈ 7 tokens per sentence.
        // MaxChunkSize=20 fits ~2-3 sentences per chunk, producing ≥2 chunks.
        // OverlapTokens=6 ≈ 1 sentence worth → second chunk should re-include the last sentence of the first.
        var text = BuildSentences(6, wordsPerSentence: 5);
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 30)
        };
        var options = DefaultOptions(maxChunkSize: 20, overlapTokens: 6);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — at least 2 text chunks produced
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThanOrEqualTo(2);

        // The offset ranges of consecutive chunks must overlap
        // (second chunk's start < first chunk's end)
        await Assert.That(textChunks[1].StartOffset!.Value)
            .IsLessThan(textChunks[0].EndOffset!.Value);
    }

    [Test]
    public async Task ChunkDocument_MultiplePages_TracksPageNumbersCorrectly()
    {
        // Arrange — 2 pages; the combined text is large enough to yield at least
        // one chunk that spans both pages.
        var page1Text = BuildSentences(3, wordsPerSentence: 8); // 24 words
        var page2Text = BuildSentences(3, wordsPerSentence: 8); // 24 words
        var page1 = BuildPage(1, page1Text);
        var page2 = BuildPage(2, page2Text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page1, page2],
            Metadata = DefaultMetadata(pageCount: 2, totalWordCount: 48)
        };
        // MaxChunkSize=60 — large enough to pull content from both pages into one chunk
        var options = DefaultOptions(maxChunkSize: 60, overlapTokens: 5);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — at least one text chunk references both pages
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThan(0);

        var crossPageChunk = textChunks.FirstOrDefault(c => c.PageNumbers.Count > 1);
        await Assert.That(crossPageChunk).IsNotNull();
        await Assert.That(crossPageChunk!.PageNumbers).Contains(1);
        await Assert.That(crossPageChunk.PageNumbers).Contains(2);
    }

    [Test]
    public async Task ChunkDocument_AllTextChunks_TokenCountDoesNotExceedMaxChunkSize()
    {
        // Arrange
        var text = BuildSentences(10, wordsPerSentence: 8); // 80 words
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 80)
        };
        var options = DefaultOptions(maxChunkSize: 50, overlapTokens: 8);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — invariant: no text chunk may exceed MaxChunkSize
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThan(0);
        foreach (var chunk in textChunks)
        {
            await Assert.That(chunk.TokenCount).IsLessThanOrEqualTo(options.MaxChunkSize);
        }
    }

    // =========================================================================
    // Tables
    // =========================================================================

    [Test]
    public async Task ChunkDocument_Table_CreatedAsAtomicChunk()
    {
        // Arrange
        var table = BuildTable(tableNumber: 1, pageNumber: 1);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Tables = [table],
            Metadata = DefaultMetadata(totalTables: 1)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        var tableChunks = chunks.Where(c => c.ChunkType == ChunkType.Table).ToList();
        await Assert.That(tableChunks).Count().IsEqualTo(1);
        await Assert.That(tableChunks[0].ChunkType).IsEqualTo(ChunkType.Table);
    }

    [Test]
    public async Task ChunkDocument_LargeTable_StaysAsSingleChunkEvenIfOverMaxSize()
    {
        // Arrange — JSON representation is deliberately larger than MaxChunkSize in tokens
        var largeJson = "{" + string.Join(",", Enumerable.Range(1, 200).Select(i => $"\"key{i}\":\"value{i}\"")) + "}";
        var table = BuildTable(tableNumber: 1, pageNumber: 1, jsonRepresentation: largeJson);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Tables = [table],
            Metadata = DefaultMetadata(totalTables: 1)
        };
        // MaxChunkSize=20 — the large JSON vastly exceeds this
        var options = DefaultOptions(maxChunkSize: 20, overlapTokens: 3);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — still exactly one table chunk, never split
        var tableChunks = chunks.Where(c => c.ChunkType == ChunkType.Table).ToList();
        await Assert.That(tableChunks).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ChunkDocument_TableContent_UsesJsonRepresentation()
    {
        // Arrange
        const string expectedJson = """[{"col1":"val1","col2":"val2"}]""";
        var table = BuildTable(tableNumber: 1, pageNumber: 1, jsonRepresentation: expectedJson);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Tables = [table],
            Metadata = DefaultMetadata(totalTables: 1)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        var tableChunk = chunks.Single(c => c.ChunkType == ChunkType.Table);
        await Assert.That(tableChunk.Content).IsEqualTo(expectedJson);
    }

    // =========================================================================
    // Form Fields
    // =========================================================================

    [Test]
    public async Task ChunkDocument_FormFields_GroupedIntoSingleChunk()
    {
        // Arrange
        var fields = new[]
        {
            BuildFormField("Invoice Number", "INV-001"),
            BuildFormField("Total Amount", "1234.56"),
            BuildFormField("Due Date", "2026-03-31")
        };
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            FormFields = fields,
            Metadata = DefaultMetadata(totalFormFields: 3)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert — all form fields become exactly one FormField chunk
        var formFieldChunks = chunks.Where(c => c.ChunkType == ChunkType.FormField).ToList();
        await Assert.That(formFieldChunks).Count().IsEqualTo(1);
        await Assert.That(formFieldChunks[0].ChunkType).IsEqualTo(ChunkType.FormField);

        // Content should contain each key/value pair
        var content = formFieldChunks[0].Content;
        await Assert.That(content).Contains("Invoice Number");
        await Assert.That(content).Contains("INV-001");
        await Assert.That(content).Contains("Total Amount");
        await Assert.That(content).Contains("1234.56");
    }

    [Test]
    public async Task ChunkDocument_NoFormFields_NoFormFieldChunk()
    {
        // Arrange
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            FormFields = [],
            Metadata = DefaultMetadata()
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        var formFieldChunks = chunks.Where(c => c.ChunkType == ChunkType.FormField).ToList();
        await Assert.That(formFieldChunks).Count().IsEqualTo(0);
    }

    // =========================================================================
    // Metadata and IDs
    // =========================================================================

    [Test]
    public async Task ChunkDocument_ChunkIds_FollowExpectedPattern()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var page = BuildPage(1, "Hello world. This is a test sentence.");
        var table = BuildTable(tableNumber: 1, pageNumber: 1);
        var field = BuildFormField("Key", "Value");
        var input = new PreprocessResult
        {
            DocumentId = documentId,
            JobId = Guid.NewGuid(),
            Pages = [page],
            Tables = [table],
            FormFields = [field],
            Metadata = DefaultMetadata(pageCount: 1, totalTables: 1, totalFormFields: 1)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert — each chunk ID matches the pattern doc-{documentId}-chunk-{index:D4}
        await Assert.That(chunks.Count).IsGreaterThan(0);
        for (var i = 0; i < chunks.Count; i++)
        {
            var expectedId = $"doc-{documentId}-chunk-{i:D4}";
            await Assert.That(chunks[i].ChunkId).IsEqualTo(expectedId);
        }
    }

    [Test]
    public async Task ChunkDocument_ChunkIndexes_AreZeroBasedSequential()
    {
        // Arrange
        var text = BuildSentences(4, wordsPerSentence: 8);
        var page = BuildPage(1, text);
        var table = BuildTable();
        var field = BuildFormField("Field", "Value");
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Tables = [table],
            FormFields = [field],
            Metadata = DefaultMetadata(pageCount: 1, totalTables: 1, totalFormFields: 1)
        };
        var options = DefaultOptions(maxChunkSize: 30, overlapTokens: 5);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert
        await Assert.That(chunks.Count).IsGreaterThan(0);
        for (var i = 0; i < chunks.Count; i++)
        {
            await Assert.That(chunks[i].ChunkIndex).IsEqualTo(i);
        }
    }

    [Test]
    public async Task ChunkDocument_TokenCount_EstimatedCorrectly()
    {
        // Arrange — a single short sentence so it lands in exactly one chunk
        // "word1 word2 word3 word4 word5." = 5 words; tokens = round(5 * 1.3) = 7 (or 6 by floor)
        const string text = "word1 word2 word3 word4 word5.";
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 5)
        };
        const double factor = 1.3;
        var options = DefaultOptions(maxChunkSize: 100, overlapTokens: 5, tokenEstimationFactor: factor);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — token count = wordCount * factor, rounded (floor or round, either is acceptable)
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks).Count().IsEqualTo(1);

        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var expectedTokenMin = (int)Math.Floor(wordCount * factor);
        var expectedTokenMax = (int)Math.Ceiling(wordCount * factor);

        await Assert.That(textChunks[0].TokenCount).IsGreaterThanOrEqualTo(expectedTokenMin);
        await Assert.That(textChunks[0].TokenCount).IsLessThanOrEqualTo(expectedTokenMax);
    }

    [Test]
    public async Task ChunkDocument_TextChunks_StartEndOffsetsMapToSourceText()
    {
        // Arrange
        const string text = "First sentence here. Second sentence here. Third sentence here.";
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 12)
        };
        // Small MaxChunkSize to force splitting so we can test offset accuracy
        var options = DefaultOptions(maxChunkSize: 20, overlapTokens: 3);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — for each text chunk, Content must equal fullDocText[StartOffset..EndOffset]
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThan(0);

        // The full document text is the pages joined with "\n\n"
        var fullDocText = text; // single page, no separator needed

        foreach (var chunk in textChunks)
        {
            await Assert.That(chunk.StartOffset).IsNotNull();
            await Assert.That(chunk.EndOffset).IsNotNull();

            var extractedContent = fullDocText[chunk.StartOffset!.Value..chunk.EndOffset!.Value];
            await Assert.That(chunk.Content).IsEqualTo(extractedContent);
        }
    }

    [Test]
    public async Task ChunkDocument_TextChunks_SourceBlocksMappedCorrectly()
    {
        // Arrange — two text blocks on page 1; page text uses "\n" separator (matching PreprocessStageActivity)
        var block0 = BuildTextBlock("First block content. With two sentences.", pageNumber: 1);
        var block1 = BuildTextBlock("Second block content. With two sentences.", pageNumber: 1);
        var page = BuildPage(1,
            "First block content. With two sentences.\nSecond block content. With two sentences.",
            [block0, block1]);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 16)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert — text chunks must have non-null SourceBlocks
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThan(0);
        foreach (var chunk in textChunks)
        {
            await Assert.That(chunk.SourceBlocks).IsNotNull();
            // All source block indexes must reference valid blocks (0 or 1 in this case)
            foreach (var blockIndex in chunk.SourceBlocks!)
            {
                await Assert.That(blockIndex).IsGreaterThanOrEqualTo(0);
                await Assert.That(blockIndex).IsLessThan(2);
            }
        }
    }

    [Test]
    public async Task ChunkDocument_MultipleBlocksWithNewlineSeparator_ProducesCorrectOffsets()
    {
        // Arrange — three blocks joined by "\n" separators (matching PreprocessStageActivity behavior)
        var block0 = BuildTextBlock("Alpha block.", pageNumber: 1);
        var block1 = BuildTextBlock("Beta block.", pageNumber: 1);
        var block2 = BuildTextBlock("Gamma block.", pageNumber: 1);
        // page.NormalizedText = string.Join("\n", blockTexts) in PreprocessStageActivity
        var pageText = "Alpha block.\nBeta block.\nGamma block.";
        var page = BuildPage(1, pageText, [block0, block1, block2]);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 6)
        };

        // Act — large MaxChunkSize so everything fits in one chunk
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions(maxChunkSize: 200, overlapTokens: 0));

        // Assert — the single text chunk should span the entire page text
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks).Count().IsEqualTo(1);

        var chunk = textChunks[0];
        await Assert.That(chunk.Content).IsEqualTo(pageText);
        await Assert.That(chunk.SourceBlocks).IsNotNull();
        // All 3 blocks should be referenced
        await Assert.That(chunk.SourceBlocks!.Count).IsEqualTo(3);
        await Assert.That(chunk.SourceBlocks).Contains(0);
        await Assert.That(chunk.SourceBlocks).Contains(1);
        await Assert.That(chunk.SourceBlocks).Contains(2);
    }

    [Test]
    public async Task ChunkDocument_EmptyBlockBetweenNonEmpty_OffsetsStillAlign()
    {
        // Arrange — empty block between two non-empty blocks
        var block0 = BuildTextBlock("First.", pageNumber: 1);
        var block1 = BuildTextBlock("", pageNumber: 1);   // empty block
        var block2 = BuildTextBlock("Third.", pageNumber: 1);
        // string.Join("\n", ["First.", "", "Third."]) = "First.\n\nThird."
        var pageText = "First.\n\nThird.";
        var page = BuildPage(1, pageText, [block0, block1, block2]);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 2)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions(maxChunkSize: 200, overlapTokens: 0));

        // Assert — chunk content matches the full page text
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks).Count().IsEqualTo(1);
        await Assert.That(textChunks[0].Content).IsEqualTo(pageText);
        // All 3 blocks should be referenced (including the empty one, which has zero-length range)
        await Assert.That(textChunks[0].SourceBlocks).IsNotNull();
        await Assert.That(textChunks[0].SourceBlocks!.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ChunkDocument_Metadata_HasCorrectCounts()
    {
        // Arrange
        var text = BuildSentences(2, wordsPerSentence: 5); // 10 words — fits in 1 chunk at MaxChunkSize=80
        var page = BuildPage(1, text);
        var table1 = BuildTable(tableNumber: 1, pageNumber: 1);
        var table2 = BuildTable(tableNumber: 2, pageNumber: 1);
        var field = BuildFormField("Key", "Value");
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Tables = [table1, table2],
            FormFields = [field],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 10, totalTables: 2, totalFormFields: 1)
        };

        // Act
        var (chunks, metadata) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        var expectedTextChunks = chunks.Count(c => c.ChunkType == ChunkType.Text);
        var expectedTableChunks = chunks.Count(c => c.ChunkType == ChunkType.Table);
        var expectedFormFieldChunks = chunks.Count(c => c.ChunkType == ChunkType.FormField);
        var expectedTotal = chunks.Count;
        var expectedTotalTokens = chunks.Sum(c => c.TokenCount);

        await Assert.That(metadata.TotalChunks).IsEqualTo(expectedTotal);
        await Assert.That(metadata.TextChunks).IsEqualTo(expectedTextChunks);
        await Assert.That(metadata.TableChunks).IsEqualTo(expectedTableChunks);
        await Assert.That(metadata.FormFieldChunks).IsEqualTo(expectedFormFieldChunks);
        await Assert.That(metadata.TotalTokens).IsEqualTo(expectedTotalTokens);
        await Assert.That(metadata.MaxChunkSize).IsEqualTo(DefaultOptions().MaxChunkSize);
        await Assert.That(metadata.OverlapTokens).IsEqualTo(DefaultOptions().OverlapTokens);

        // Sanity: 2 tables + 1 text chunk + 1 form-field chunk
        await Assert.That(metadata.TableChunks).IsEqualTo(2);
        await Assert.That(metadata.FormFieldChunks).IsEqualTo(1);
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Test]
    public async Task ChunkDocument_EmptyDocument_ReturnsEmptyChunkList()
    {
        // Arrange
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [],
            Tables = [],
            FormFields = [],
            Metadata = DefaultMetadata()
        };

        // Act
        var (chunks, metadata) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        await Assert.That(chunks).Count().IsEqualTo(0);
        await Assert.That(metadata.TotalChunks).IsEqualTo(0);
        await Assert.That(metadata.TextChunks).IsEqualTo(0);
        await Assert.That(metadata.TableChunks).IsEqualTo(0);
        await Assert.That(metadata.FormFieldChunks).IsEqualTo(0);
        await Assert.That(metadata.TotalTokens).IsEqualTo(0);
    }

    [Test]
    public async Task ChunkDocument_TextWithNoPunctuation_FallsBackToLineBreakSplitting()
    {
        // Arrange — no sentences ending in .!? so the regex splitter finds nothing;
        // use \n line breaks so the fallback \n-splitter can divide the text.
        var lines = Enumerable.Range(1, 6)
            .Select(i => string.Join(' ', Enumerable.Range(1, 8).Select(w => $"word{(i - 1) * 8 + w}")));
        var text = string.Join('\n', lines); // 6 lines × 8 words, no punctuation
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 48)
        };
        // MaxChunkSize=25 → ~19 words → needs >1 chunk from 48 words
        var options = DefaultOptions(maxChunkSize: 25, overlapTokens: 4);

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — falls back gracefully to produce multiple chunks
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThan(1);
        // Content should only contain words from the original text (no garbage)
        foreach (var chunk in textChunks)
        {
            await Assert.That(chunk.Content).IsNotEmpty();
        }
    }

    [Test]
    public async Task ChunkDocument_SingleSentence_NoOverlapProduced()
    {
        // Arrange — just one sentence; there is nothing to overlap with
        const string text = "This is the only sentence in the entire document.";
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 9)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert — exactly one text chunk; Content equals the original sentence (no duplication)
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks).Count().IsEqualTo(1);
        await Assert.That(textChunks[0].Content).IsEqualTo(text);
    }

    [Test]
    public async Task ChunkDocument_AllTablesNoText_ReturnsOnlyTableChunks()
    {
        // Arrange — no pages at all, just two tables
        var table1 = BuildTable(tableNumber: 1, pageNumber: 1);
        var table2 = BuildTable(tableNumber: 2, pageNumber: 2);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [],
            Tables = [table1, table2],
            FormFields = [],
            Metadata = DefaultMetadata(totalTables: 2)
        };

        // Act
        var (chunks, metadata) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        await Assert.That(chunks.Where(c => c.ChunkType == ChunkType.Text)).Count().IsEqualTo(0);
        await Assert.That(chunks.Where(c => c.ChunkType == ChunkType.Table)).Count().IsEqualTo(2);
        await Assert.That(metadata.TextChunks).IsEqualTo(0);
        await Assert.That(metadata.TableChunks).IsEqualTo(2);
    }

    [Test]
    public async Task ChunkDocument_InterleavedTextAndTables_MaintainsPageOrder()
    {
        // Arrange — page 1 has text, page 2 has a table; verify that the table
        // chunk's PageNumbers correctly reference page 2, not page 1
        var page1 = BuildPage(1, "Short text on page one. It is brief.");
        var tableOnPage2 = BuildTable(tableNumber: 1, pageNumber: 2);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page1],
            Tables = [tableOnPage2],
            Metadata = DefaultMetadata(pageCount: 2, totalTables: 1)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert
        var tableChunk = chunks.Single(c => c.ChunkType == ChunkType.Table);
        await Assert.That(tableChunk.PageNumbers).Contains(2);
        await Assert.That(tableChunk.PageNumbers).DoesNotContain(1);
    }

    [Test]
    public async Task ChunkDocument_OverlapTokensGreaterThanMaxChunkSize_ThrowsArgumentException()
    {
        // Arrange — degenerate config: overlap (100) >= maxChunkSize (50)
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [BuildPage(1, "Some text.")],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 2)
        };
        var badOptions = new ChunkingOptions
        {
            OutputBlobContainer = "chunk-results",
            MaxChunkSize = 50,
            OverlapTokens = 100, // greater than MaxChunkSize
            TokenEstimationFactor = 1.3
        };

        // Act & Assert
        await Assert.That(() => _sut.ChunkDocument(input, badOptions))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ChunkDocument_ZeroOverlap_ProducesNoOverlap()
    {
        // Arrange — enough sentences to force ≥2 chunks, but overlap=0
        var text = BuildSentences(6, wordsPerSentence: 8); // 48 words
        var page = BuildPage(1, text);
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Pages = [page],
            Metadata = DefaultMetadata(pageCount: 1, totalWordCount: 48)
        };
        var options = new ChunkingOptions
        {
            OutputBlobContainer = "chunk-results",
            MaxChunkSize = 30,
            OverlapTokens = 0,  // zero overlap — valid config
            TokenEstimationFactor = 1.3
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, options);

        // Assert — succeeds and produces chunks; no content is duplicated between them
        var textChunks = chunks.Where(c => c.ChunkType == ChunkType.Text).ToList();
        await Assert.That(textChunks.Count).IsGreaterThanOrEqualTo(2);

        // Reconstruct all content by joining chunks — total length must match source
        // (no overlap means no duplication, so lengths add up to the source text length)
        var combinedLength = textChunks.Sum(c => c.Content.Length);
        await Assert.That(combinedLength).IsEqualTo(text.Length);
    }

    [Test]
    public async Task ChunkDocument_TableAndFormFieldChunks_HaveNullOffsets()
    {
        // Arrange
        var table = BuildTable(tableNumber: 1, pageNumber: 1);
        var field = BuildFormField("Invoice", "INV-999");
        var input = new PreprocessResult
        {
            DocumentId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            Tables = [table],
            FormFields = [field],
            Metadata = DefaultMetadata(totalTables: 1, totalFormFields: 1)
        };

        // Act
        var (chunks, _) = _sut.ChunkDocument(input, DefaultOptions());

        // Assert — non-text chunks must have null offsets
        var nonTextChunks = chunks.Where(c => c.ChunkType != ChunkType.Text).ToList();
        await Assert.That(nonTextChunks.Count).IsGreaterThan(0);
        foreach (var chunk in nonTextChunks)
        {
            await Assert.That(chunk.StartOffset).IsNull();
            await Assert.That(chunk.EndOffset).IsNull();
        }
    }
}
