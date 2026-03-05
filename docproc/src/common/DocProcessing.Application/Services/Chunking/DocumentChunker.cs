using System.Text;
using System.Text.RegularExpressions;
using DocProcessing.Application.Configuration;
using DocProcessing.Application.Models.Chunking;
using DocProcessing.Application.Models.Preprocessing;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Services.Chunking;

/// <summary>
/// Splits a preprocessed document into text, table, and form-field chunks.
/// This class is thread-safe: all methods are pure functions with no shared mutable state.
/// </summary>
public sealed partial class DocumentChunker : IDocumentChunker
{
    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z\n])")]
    private static partial Regex SentenceBoundaryRegex();

    /// <inheritdoc/>
    public (IReadOnlyList<DocumentChunk> Chunks, ChunkMetadata Metadata) ChunkDocument(
        PreprocessResult input, ChunkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxChunkSize <= 0)
        {
            throw new ArgumentException(
                $"MaxChunkSize ({options.MaxChunkSize}) must be greater than zero.",
                nameof(options));
        }

        if (options.TokenEstimationFactor <= 0)
        {
            throw new ArgumentException(
                $"TokenEstimationFactor ({options.TokenEstimationFactor}) must be greater than zero.",
                nameof(options));
        }

        if (options.OverlapTokens >= options.MaxChunkSize)
        {
            throw new ArgumentException(
                $"OverlapTokens ({options.OverlapTokens}) must be less than MaxChunkSize ({options.MaxChunkSize}).",
                nameof(options));
        }

        var (fullDocText, pageRanges, blockRanges) = BuildFullDocumentText(input.Pages);

        var textChunks = BuildTextChunks(fullDocText, pageRanges, blockRanges, input.DocumentId, options);
        var tableChunks = BuildTableChunks(input.Tables, input.DocumentId, options);
        var formFieldChunk = BuildFormFieldChunk(input.FormFields, input.DocumentId, options);

        var allChunks = new List<DocumentChunk>(textChunks.Count + tableChunks.Count + (formFieldChunk is null ? 0 : 1));
        allChunks.AddRange(textChunks);
        allChunks.AddRange(tableChunks);
        if (formFieldChunk is not null)
        {
            allChunks.Add(formFieldChunk);
        }

        var finalChunks = AssignIdsAndIndexes(allChunks, input.DocumentId);

        var metadata = new ChunkMetadata
        {
            TotalChunks = finalChunks.Count,
            TextChunks = textChunks.Count,
            TableChunks = tableChunks.Count,
            FormFieldChunks = formFieldChunk is null ? 0 : 1,
            TotalTokens = finalChunks.Sum(c => c.TokenCount),
            MaxChunkSize = options.MaxChunkSize,
            OverlapTokens = options.OverlapTokens
        };

        return (finalChunks, metadata);
    }

    private static (string FullText, List<PageRange> PageRanges, List<BlockRange> BlockRanges)
        BuildFullDocumentText(IReadOnlyList<PreprocessedPage> pages)
    {
        if (pages.Count == 0)
        {
            return (string.Empty, [], []);
        }

        var sb = new StringBuilder();
        var pageRanges = new List<PageRange>(pages.Count);
        var blockRanges = new List<BlockRange>();
        int globalBlockIndex = 0;

        for (int i = 0; i < pages.Count; i++)
        {
            var page = pages[i];

            if (i > 0)
            {
                sb.Append("\n\n");
            }

            int pageStart = sb.Length;

            // Track block char ranges within the page text.
            // Page text is constructed via string.Join("\n", allBlockTexts) in PreprocessStageActivity,
            // so every block after the first is preceded by a "\n" separator.
            int searchFrom = pageStart;
            for (int blockIdx = 0; blockIdx < page.TextBlocks.Count; blockIdx++)
            {
                var block = page.TextBlocks[blockIdx];
                string blockText = block.NormalizedText;

                // Account for "\n" separator between blocks
                if (blockIdx > 0)
                {
                    searchFrom += 1;
                }

                if (string.IsNullOrEmpty(blockText))
                {
                    blockRanges.Add(new BlockRange(globalBlockIndex, searchFrom, searchFrom, page.PageNumber));
                    globalBlockIndex++;
                    continue;
                }

                blockRanges.Add(new BlockRange(globalBlockIndex, searchFrom, searchFrom + blockText.Length, page.PageNumber));
                searchFrom += blockText.Length;
                globalBlockIndex++;
            }

            sb.Append(page.NormalizedText);

            int pageEnd = sb.Length;
            pageRanges.Add(new PageRange(page.PageNumber, pageStart, pageEnd));
        }

        return (sb.ToString(), pageRanges, blockRanges);
    }

    private static List<DocumentChunk> BuildTextChunks(
        string fullDocText,
        List<PageRange> pageRanges,
        List<BlockRange> blockRanges,
        Guid documentId,
        ChunkingOptions options)
    {
        if (string.IsNullOrEmpty(fullDocText))
        {
            return [];
        }

        var sentences = SplitIntoSentences(fullDocText);
        var chunks = new List<DocumentChunk>();

        int sentenceStart = 0;

        while (sentenceStart < sentences.Count)
        {
            int chunkCharStart = sentences[sentenceStart].StartOffset;
            int accumulatedTokens = 0;
            int sentenceEnd = sentenceStart;

            // Accumulate sentences until adding the next one would exceed MaxChunkSize.
            // A single sentence that exceeds MaxChunkSize is always included to avoid infinite loops.
            while (sentenceEnd < sentences.Count)
            {
                int sentenceTokens = EstimateTokens(sentences[sentenceEnd].Text, options.TokenEstimationFactor);

                if (accumulatedTokens + sentenceTokens > options.MaxChunkSize && accumulatedTokens > 0)
                {
                    break;
                }

                accumulatedTokens += sentenceTokens;
                sentenceEnd++;
            }

            // sentenceEnd is exclusive — sentences [sentenceStart, sentenceEnd) form this chunk
            int chunkCharEnd = sentences[sentenceEnd - 1].EndOffset;
            string content = fullDocText[chunkCharStart..chunkCharEnd];

            var pageNumbers = GetPageNumbers(chunkCharStart, chunkCharEnd, pageRanges);
            var sourceBlocks = GetSourceBlocks(chunkCharStart, chunkCharEnd, blockRanges);
            int tokenCount = EstimateTokens(content, options.TokenEstimationFactor);

            chunks.Add(new DocumentChunk
            {
                ChunkId = string.Empty,
                ChunkIndex = 0,
                DocumentId = documentId,
                ChunkType = ChunkType.Text,
                Content = content,
                StartOffset = chunkCharStart,
                EndOffset = chunkCharEnd,
                PageNumbers = pageNumbers,
                SourceBlocks = sourceBlocks,
                TokenCount = tokenCount
            });

            // Determine overlap start for next chunk: walk backward from sentenceEnd
            // until accumulated overlap tokens reach OverlapTokens
            if (options.OverlapTokens > 0)
            {
                int overlapSentenceStart = sentenceEnd - 1;
                int overlapTokens = EstimateTokens(sentences[overlapSentenceStart].Text, options.TokenEstimationFactor);

                while (overlapSentenceStart > sentenceStart && overlapTokens < options.OverlapTokens)
                {
                    overlapSentenceStart--;
                    overlapTokens += EstimateTokens(sentences[overlapSentenceStart].Text, options.TokenEstimationFactor);
                }

                // Guard: always advance at least one sentence to prevent infinite loop
                // (when only 1 sentence fits per chunk, overlap would set sentenceStart back to itself)
                sentenceStart = Math.Max(overlapSentenceStart, sentenceStart + 1);
            }
            else
            {
                sentenceStart = sentenceEnd;
            }
        }

        return chunks;
    }

    private static List<SentenceSpan> SplitIntoSentences(string text)
    {
        var result = new List<SentenceSpan>();

        var matches = SentenceBoundaryRegex().Matches(text);

        if (matches.Count is 0)
        {
            // Fallback 1: split on newlines
            var lines = SplitOnDelimiter(text, '\n');
            if (lines.Count > 1)
            {
                return lines;
            }

            // Fallback 2: split on whitespace boundaries at fixed character positions
            return SplitOnWhitespaceBoundaries(text);
        }

        int currentStart = 0;
        foreach (Match match in matches)
        {
            // The sentence text includes everything up to where the next sentence begins,
            // i.e. up to but not including the first char of the next sentence.
            // The regex splits on the whitespace between sentences, so the sentence ends
            // at match.Index (just after the punctuation) and the next starts at match.Index + match.Length.
            int rawEnd = match.Index + match.Length; // start of next sentence
            string sentenceText = text[currentStart..rawEnd];
            result.Add(new SentenceSpan(sentenceText, currentStart, rawEnd));
            currentStart = rawEnd;
        }

        // Last sentence (remainder)
        if (currentStart < text.Length)
        {
            result.Add(new SentenceSpan(text[currentStart..], currentStart, text.Length));
        }

        return result;
    }

    private static List<SentenceSpan> SplitOnDelimiter(string text, char delimiter)
    {
        var result = new List<SentenceSpan>();
        int start = 0;

        while (start < text.Length)
        {
            int idx = text.IndexOf(delimiter, start);
            if (idx < 0)
            {
                result.Add(new SentenceSpan(text[start..], start, text.Length));
                break;
            }

            // Include the delimiter in the span
            int end = idx + 1;
            result.Add(new SentenceSpan(text[start..end], start, end));
            start = end;
        }

        return result;
    }

    private static List<SentenceSpan> SplitOnWhitespaceBoundaries(string text)
    {
        // Walk the text and find the last whitespace position within each window,
        // producing spans that together cover the entire text exactly.
        const int windowSize = 200;
        var result = new List<SentenceSpan>();
        int start = 0;

        while (start < text.Length)
        {
            if (start + windowSize >= text.Length)
            {
                result.Add(new SentenceSpan(text[start..], start, text.Length));
                break;
            }

            int end = start + windowSize;

            // Walk back to find a whitespace boundary
            while (end > start + 1 && !char.IsWhiteSpace(text[end - 1]))
            {
                end--;
            }

            // If no whitespace found in window, force cut at window boundary
            // to avoid 1-char-at-a-time degradation
            if (end <= start + 1)
            {
                end = start + windowSize;
            }

            result.Add(new SentenceSpan(text[start..end], start, end));
            start = end;
        }

        return result;
    }

    private static List<DocumentChunk> BuildTableChunks(
        IReadOnlyList<StructuredTable> tables,
        Guid documentId,
        ChunkingOptions options)
    {
        var chunks = new List<DocumentChunk>(tables.Count);

        foreach (var table in tables.OrderBy(t => t.PageNumber).ThenBy(t => t.TableNumber))
        {
            string content = table.JsonRepresentation;
            chunks.Add(new DocumentChunk
            {
                ChunkId = string.Empty,
                ChunkIndex = 0,
                DocumentId = documentId,
                ChunkType = ChunkType.Table,
                Content = content,
                StartOffset = null,
                EndOffset = null,
                PageNumbers = [table.PageNumber],
                SourceBlocks = null,
                TokenCount = EstimateTokens(content, options.TokenEstimationFactor)
            });
        }

        return chunks;
    }

    private static DocumentChunk? BuildFormFieldChunk(
        IReadOnlyList<NormalizedFormField> fields,
        Guid documentId,
        ChunkingOptions options)
    {
        if (fields.Count == 0)
        {
            return null;
        }

        string content = string.Join('\n', fields.Select(f => $"{f.Key}: {f.NormalizedValue}"));
        var pageNumbers = fields
            .Select(f => f.PageNumber)
            .Distinct()
            .Order()
            .ToList();

        return new DocumentChunk
        {
            ChunkId = string.Empty,
            ChunkIndex = 0,
            DocumentId = documentId,
            ChunkType = ChunkType.FormField,
            Content = content,
            StartOffset = null,
            EndOffset = null,
            PageNumbers = pageNumbers,
            SourceBlocks = null,
            TokenCount = EstimateTokens(content, options.TokenEstimationFactor)
        };
    }

    private static List<DocumentChunk> AssignIdsAndIndexes(List<DocumentChunk> chunks, Guid documentId)
    {
        var result = new List<DocumentChunk>(chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            result.Add(chunk with
            {
                ChunkId = $"doc-{documentId}-chunk-{i:D4}",
                ChunkIndex = i
            });
        }

        return result;
    }

    private static IReadOnlyList<int> GetPageNumbers(int startOffset, int endOffset, List<PageRange> pageRanges)
    {
        var pages = new List<int>();

        foreach (var range in pageRanges)
        {
            if (startOffset < range.EndOffset && endOffset > range.StartOffset)
            {
                pages.Add(range.PageNumber);
            }
        }

        return pages;
    }

    private static IReadOnlyList<int> GetSourceBlocks(int startOffset, int endOffset, List<BlockRange> blockRanges)
    {
        var blocks = new List<int>();

        foreach (var block in blockRanges)
        {
            if (startOffset < block.EndOffset && endOffset > block.StartOffset)
            {
                blocks.Add(block.BlockIndex);
            }
        }

        return blocks;
    }

    private static int EstimateTokens(string text, double factor)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        // Count words without allocating a string array — this method is on the hot path
        int wordCount = 0;
        bool inWord = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (inWord) wordCount++;
                inWord = false;
            }
            else
            {
                inWord = true;
            }
        }

        if (inWord) wordCount++;

        return (int)Math.Ceiling(wordCount * factor);
    }

    private readonly record struct PageRange(int PageNumber, int StartOffset, int EndOffset);

    private readonly record struct BlockRange(int BlockIndex, int StartOffset, int EndOffset, int PageNumber);

    private readonly record struct SentenceSpan(string Text, int StartOffset, int EndOffset);
}
