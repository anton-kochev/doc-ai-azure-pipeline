using DocProcessing.Domain.Exceptions;

namespace DocProcessing.Application.Tests.Exceptions;

public sealed class RetrievalFailedExceptionTests
{
    private static readonly Guid TestDocumentId = Guid.NewGuid();
    private const string TestQueryText = "find invoice totals";
    private const string TestMessage = "Vector store unavailable";

    [Test]
    public async Task Constructor_SetsDocumentId()
    {
        var exception = new RetrievalFailedException(TestDocumentId, TestQueryText, TestMessage);

        await Assert.That(exception.DocumentId).IsEqualTo(TestDocumentId);
    }

    [Test]
    public async Task Constructor_SetsQueryText()
    {
        var exception = new RetrievalFailedException(TestDocumentId, TestQueryText, TestMessage);

        await Assert.That(exception.QueryText).IsEqualTo(TestQueryText);
    }

    [Test]
    public async Task Constructor_SetsMessage()
    {
        var exception = new RetrievalFailedException(TestDocumentId, TestQueryText, TestMessage);

        await Assert.That(exception.Message).IsEqualTo(TestMessage);
    }

    [Test]
    public async Task Constructor_WithInnerException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("connection failed");

        var exception = new RetrievalFailedException(
            TestDocumentId, TestQueryText, TestMessage, inner);

        await Assert.That(exception.InnerException).IsEqualTo(inner);
        await Assert.That(exception.DocumentId).IsEqualTo(TestDocumentId);
        await Assert.That(exception.QueryText).IsEqualTo(TestQueryText);
        await Assert.That(exception.Message).IsEqualTo(TestMessage);
    }
}
