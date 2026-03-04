using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.EndToEnd.Tests.Helpers;

/// <summary>
/// Helper methods for asserting document and job state in the database.
/// </summary>
public static class DocumentAssertions
{
    public static async Task<ProcessJob> GetJobAsync(IApplicationDbContext dbContext, Guid jobId)
    {
        ProcessJob? job = await dbContext.ProcessJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        if (job is null)
            throw new InvalidOperationException($"Job {jobId} not found in database");

        return job;
    }

    public static async Task<Document> GetDocumentAsync(IApplicationDbContext dbContext, Guid documentId)
    {
        Document? doc = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (doc is null)
            throw new InvalidOperationException($"Document {documentId} not found in database");

        return doc;
    }

    public static async Task AssertJobStatusAsync(
        IApplicationDbContext dbContext,
        Guid jobId,
        ProcessJobStatus expectedStatus)
    {
        ProcessJob job = await GetJobAsync(dbContext, jobId);
        await Assert.That(job.Status).IsEqualTo(expectedStatus);
    }

    public static async Task AssertJobAttemptsAsync(
        IApplicationDbContext dbContext,
        Guid jobId,
        int expectedAttempts)
    {
        ProcessJob job = await GetJobAsync(dbContext, jobId);
        await Assert.That(job.Attempts).IsEqualTo(expectedAttempts);
    }
}
