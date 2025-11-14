# Document AI Pipeline - Implementation Plan

**Last Updated:** 2025-11-14
**Current Phase:** Core Processing Logic (Phase 2)

---

## Project Overview

A serverless document processing pipeline using Azure Functions, Clean Architecture, and .NET 8. Processes documents through multiple stages: Upload → OCR → Preprocess → Embed → Extract → Validate → Persist → Notify.

---

## Phase Status

### Phase 1: Foundation & Exception Handling ✅ COMPLETED
- ✅ Domain exception pattern implemented
- ✅ State machine validation
- ✅ 120 tests passing
- ✅ All services using domain exceptions

### Phase 2: Core Processing Logic 🔄 IN PROGRESS (20% Complete)

#### Completed Stages:
- ✅ **Preprocess Stage** (2025-11-14)
  - Text normalization (Unicode NFC, ligatures, whitespace)
  - Table conversion (JSON/CSV with RFC 4180 compliance)
  - Form field parsing (dates, currencies, numbers)
  - 49 tests passing
  - Code quality: 8.5/10

#### Remaining Stages:

**1. Validate Stage** (NEXT - Recommended)
- Validate preprocessed data quality
- Check for minimum confidence thresholds
- Validate required fields presence
- Flag documents for manual review if needed
- Estimated effort: 1-2 days

**2. Persist Stage**
- Save final results to database
- Update ProcessJob with completion status
- Store extracted metadata
- Estimated effort: 1-2 days

**3. Extract Stage**
- Extract structured entities from text
- Extract key-value pairs
- Extract document metadata (dates, amounts, parties)
- Integration with AI/ML services
- Estimated effort: 3-5 days

**4. Embed Stage**
- Generate embeddings for semantic search
- Chunk text for embedding generation
- Store embeddings in vector database
- Integration with Azure OpenAI or similar
- Estimated effort: 2-3 days

### Phase 3: OCR Integration (Not Started)
- Replace MockOcrService with Azure Document Intelligence
- Configure OCR provider settings
- Handle OCR retries and error scenarios
- Estimated effort: 2-3 days

### Phase 4: Orchestration & Workflow (Not Started)
- Implement Durable Functions orchestrator
- Wire up all pipeline stages
- Implement retry policies
- Handle stage failures and compensation
- Estimated effort: 3-5 days

### Phase 5: Testing & Quality (Not Started)
- Integration tests for full pipeline
- Performance testing
- Load testing
- Security testing
- Estimated effort: 5-7 days

---

## Technical Debt Backlog

### 🔴 HIGH Priority (Should Fix Before Heavy Production Use)

#### TD-001: Text Normalization Performance Optimization
**Component:** `TextNormalizer.cs:53-58`
**Issue:** Multiple string allocations in `NormalizeText()` cause GC pressure on large documents.
**Impact:** Performance degradation with documents containing thousands of text blocks.

**Details:**
- Current implementation: Multiple `Split()` and `Join()` operations per text block
- Problem: For a 100-page document with 50 text blocks/page = 5,000 allocations
- Recommended fix: Use `StringBuilder` or `Span<T>` to reduce allocations

**Recommendation:**
```csharp
// Option 1: Use LINQ with StringSplitOptions (efficient)
var normalizedLines = text.Split('\n')
    .Select(line => MultipleWhitespaceRegex().Replace(line, " ").Trim());
return string.Join('\n', normalizedLines);

// Option 2: StringBuilder (manual control)
var sb = new StringBuilder(text.Length);
for (int i = 0; i < lines.Length; i++)
{
    if (i > 0) sb.Append('\n');
    var trimmed = MultipleWhitespaceRegex().Replace(lines[i], " ").Trim();
    sb.Append(trimmed);
}
return sb.ToString();
```

**Acceptance Criteria:**
- [ ] Reduce allocations by 50%+ (measure with BenchmarkDotNet)
- [ ] All existing tests pass
- [ ] Add performance benchmark tests
- [ ] Document performance characteristics

**Estimated Effort:** 2-4 hours
**Assigned To:** Unassigned
**Status:** Not Started

---

#### TD-002: Domain-Specific Exception Handling
**Component:** `PreprocessStageActivity.cs:157-166`
**Issue:** Catches generic `Exception` and wraps in generic "PREPROCESS_ERROR". Violates project error handling standards.
**Impact:** Difficult debugging, unable to distinguish retryable vs non-retryable errors.

**Details:**
- Current: All exceptions return same error code "PREPROCESS_ERROR"
- Problem: Cannot distinguish between:
  - Storage failures (retryable with backoff)
  - JSON deserialization errors (not retryable, data corruption)
  - Network timeouts (retryable immediately)
  - Missing OCR results (not retryable, upstream failure)
- Project standard: Use domain-specific exceptions (per CLAUDE.md error handling guidelines)

**Recommendation:**
```csharp
// Step 1: Create domain exceptions in DocProcessing.Domain/Exceptions/Preprocessing/
public sealed class OcrResultNotFoundException : Exception
{
    public string BlobPath { get; }
    public Guid JobId { get; }
    // Constructor with context...
}

public sealed class PreprocessingException : Exception
{
    public Guid JobId { get; }
    public string StagePhase { get; } // "TextNormalization", "TableConversion", etc.
    // Constructor with context...
}

// Step 2: Update PreprocessStageActivity catch blocks
catch (JsonException ex)
{
    LogPreprocessStageFailed(_logger, ex, context.Job.JobId, context.CorrelationId);
    return StageResult.Failure(
        errorCode: "PREPROCESS_JSON_ERROR",
        errorMessage: $"Failed to parse OCR result: {ex.Message}");
}
catch (StorageException ex) // Assuming IStorageService throws this
{
    LogPreprocessStageFailed(_logger, ex, context.Job.JobId, context.CorrelationId);
    return StageResult.Failure(
        errorCode: "PREPROCESS_STORAGE_ERROR",
        errorMessage: $"Storage operation failed: {ex.Message}");
}
catch (Exception ex)
{
    LogPreprocessStageFailed(_logger, ex, context.Job.JobId, context.CorrelationId);
    return StageResult.Failure(
        errorCode: "PREPROCESS_UNEXPECTED_ERROR",
        errorMessage: $"Unexpected preprocessing error: {ex.Message}");
}
```

**Acceptance Criteria:**
- [ ] Create 2-3 domain exception classes
- [ ] Update PreprocessStageActivity with specific catch blocks
- [ ] Update tests to verify specific error codes
- [ ] Add logging test verification for each exception type
- [ ] Document error codes in README or error catalog

**Estimated Effort:** 3-6 hours
**Assigned To:** Unassigned
**Status:** Not Started

---

### 🟡 MEDIUM Priority (Nice to Have)

#### TD-003: Configuration Options Not Utilized
**Component:** `TextNormalizer.cs:30-61`, `PreprocessOptions.cs:16-26`
**Issue:** `EnableUnicodeNormalization` and `EnableWhitespaceCleanup` flags exist but are ignored.
**Impact:** Cannot disable specific normalization steps for testing or special document types.

**Details:**
- PreprocessOptions defines:
  - `EnableUnicodeNormalization: bool`
  - `EnableWhitespaceCleanup: bool`
  - `ConvertTablesToStructured: bool` (this one IS used)
- TextNormalizer always applies all transformations regardless of settings
- Users cannot opt-out of Unicode normalization for specific scenarios

**Recommendation:**
```csharp
public sealed partial class TextNormalizer : ITextNormalizer
{
    private readonly PreprocessOptions _options;

    public TextNormalizer(IOptions<PreprocessOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Always normalize newlines (required for consistency)
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        if (_options.EnableWhitespaceCleanup)
        {
            text = MultipleNewlinesRegex().Replace(text, "\n\n");
        }

        if (_options.EnableUnicodeNormalization)
        {
            text = text.Normalize(NormalizationForm.FormC);
            foreach (var (ligature, expansion) in LigatureMap)
            {
                text = text.Replace(ligature, expansion);
            }
        }

        if (_options.EnableWhitespaceCleanup)
        {
            // Whitespace normalization per line...
        }

        return text;
    }
}
```

**Acceptance Criteria:**
- [ ] Inject IOptions<PreprocessOptions> into TextNormalizer
- [ ] Make Unicode normalization conditional
- [ ] Make whitespace cleanup conditional
- [ ] Add tests for each configuration combination
- [ ] Update DI registration

**Estimated Effort:** 2-3 hours
**Assigned To:** Unassigned
**Status:** Not Started

---

#### TD-004: Null Safety in Metadata Access
**Component:** `PreprocessStageActivity.cs:96-98`
**Issue:** `.ToString()` on potentially null object without null-conditional operator.
**Impact:** Potential NullReferenceException if metadata contains null values.

**Details:**
```csharp
// Current code (line 96-98):
var tenantId = context.Metadata.TryGetValue("TenantId", out var tenantIdObj)
    ? tenantIdObj.ToString()  // ⚠️ tenantIdObj could be null
    : "default";
```

**Recommendation:**
```csharp
// Option 1: Null-conditional operator
var tenantId = context.Metadata.TryGetValue("TenantId", out var tenantIdObj)
    ? tenantIdObj?.ToString() ?? "default"
    : "default";

// Option 2: Pattern matching (clearer intent)
var tenantId = context.Metadata.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is not null
    ? tenantIdObj.ToString()!
    : "default";

// Option 3: Helper method (reusable)
var tenantId = GetMetadataString(context.Metadata, "TenantId", "default");

private static string GetMetadataString(Dictionary<string, object> metadata, string key, string defaultValue)
{
    return metadata.TryGetValue(key, out var value) && value is not null
        ? value.ToString()!
        : defaultValue;
}
```

**Acceptance Criteria:**
- [ ] Add null-conditional operator or pattern matching
- [ ] Add test case for null metadata value
- [ ] Consider extracting helper method if pattern repeats

**Estimated Effort:** 15 minutes
**Assigned To:** Unassigned
**Status:** Not Started

---

#### TD-005: Word Count Performance
**Component:** `PreprocessStageActivity.cs:196`
**Issue:** Word count uses `Split()` which allocates an array per page.
**Impact:** Minor performance hit, accumulates over many pages.

**Details:**
```csharp
// Current code (line 196):
var wordCount = fullPageText.Split(new[] { ' ', '\n', '\r', '\t' },
    StringSplitOptions.RemoveEmptyEntries).Length;
```

**Recommendation:**
```csharp
// Option 1: Use regex (no array allocation)
private static readonly Regex WordBoundaryRegex = new(@"\b\w+\b", RegexOptions.Compiled);

var wordCount = WordBoundaryRegex.Matches(fullPageText).Count;

// Option 2: Manual counting with Span (zero allocation)
private static int CountWords(ReadOnlySpan<char> text)
{
    int count = 0;
    bool inWord = false;

    foreach (char c in text)
    {
        bool isWordChar = char.IsLetterOrDigit(c);
        if (isWordChar && !inWord)
        {
            count++;
            inWord = true;
        }
        else if (!isWordChar)
        {
            inWord = false;
        }
    }

    return count;
}

// Usage:
var wordCount = CountWords(fullPageText);
```

**Acceptance Criteria:**
- [ ] Replace Split-based counting with regex or Span-based approach
- [ ] Verify word count accuracy with tests
- [ ] Measure performance improvement (optional)

**Estimated Effort:** 1 hour
**Assigned To:** Unassigned
**Status:** Not Started

---

#### TD-006: Blob Path Construction Clarity
**Component:** `PreprocessStageActivity.cs:128`
**Issue:** Blob path includes container name which seems redundant.
**Impact:** Potential confusion or incorrect paths if not maintained carefully.

**Details:**
```csharp
// Current code (line 128):
var blobPath = $"{_options.OutputBlobContainer}/{tenantId}/{context.Job.DocumentId}/preprocess-result.json";

// Then passed to:
await _storageService.UploadJsonAsync(
    _options.OutputBlobContainer,  // ⚠️ Container specified twice?
    blobPath,
    preprocessResult,
    cancellationToken);
```

**Investigation Needed:**
- Check `IStorageService.UploadJsonAsync` signature and implementation
- Determine if path should include container or not
- Document expected path format

**Recommendation:**
```csharp
// If container should not be in path:
var blobPath = $"{tenantId}/{context.Job.DocumentId}/preprocess-result.json";

// OR if current approach is intentional, add clarifying comment:
// Note: Full path includes container name for blob URL structure
var blobPath = $"{_options.OutputBlobContainer}/{tenantId}/{context.Job.DocumentId}/preprocess-result.json";
```

**Acceptance Criteria:**
- [ ] Verify IStorageService contract expectations
- [ ] Update path construction or add clarifying comment
- [ ] Ensure tests verify correct blob paths

**Estimated Effort:** 30 minutes
**Assigned To:** Unassigned
**Status:** Not Started

---

## Additional Improvements (Low Priority)

### Suggestions from Code Review
- Consider CSV injection protection in `TableConverter.EscapeCsvValue()` for values starting with `=`, `+`, `-`, `@`
- Use `StringComparison.OrdinalIgnoreCase` in `FieldParser.cs:17` instead of `ToLowerInvariant()` for better performance
- Add telemetry/metrics for:
  - Number of ligatures replaced
  - Number of failed field parses
  - Average word count per page
  - Processing time per phase (normalize, tables, fields)
- Consider high-precision timing with `Stopwatch.GetTimestamp()` for performance analysis

---

## Progress Tracking

### Overall Project Completion: ~25%

**Phase Breakdown:**
- Phase 1 (Foundation): 100% ✅
- Phase 2 (Core Logic): 20% 🔄
  - Preprocess: 100% ✅
  - Validate: 0% ⏳
  - Persist: 0% ⏳
  - Extract: 0% ⏳
  - Embed: 0% ⏳
- Phase 3 (OCR): 0% ⏳
- Phase 4 (Orchestration): 0% ⏳
- Phase 5 (Testing): 0% ⏳

### Technical Debt Status
- HIGH Priority: 2 items (0% complete)
- MEDIUM Priority: 4 items (0% complete)
- LOW Priority: 4+ suggestions

---

## Next Session Recommendations

**Option A: Continue Core Logic (Recommended)**
- Implement Validate Stage (simple, builds on Preprocess)
- Estimated: 4-6 hours
- Value: Completes data quality workflow

**Option B: Address Technical Debt**
- Fix TD-001 (Performance) + TD-002 (Exceptions)
- Estimated: 5-10 hours
- Value: Production-ready Preprocess stage

**Option C: Implement Persist Stage**
- Save results to database
- Estimated: 6-8 hours
- Value: Enables end-to-end testing

**Recommendation:** Option A (Validate Stage) to maintain momentum on core functionality, then address TD-001 and TD-002 before full production deployment.

---

## Notes

- All work follows TDD approach per CLAUDE.md
- Error handling uses domain exceptions per project standards
- Code uses C# 12 features (collection expressions, required keyword)
- Source-generated logging for performance
- Clean Architecture principles maintained
