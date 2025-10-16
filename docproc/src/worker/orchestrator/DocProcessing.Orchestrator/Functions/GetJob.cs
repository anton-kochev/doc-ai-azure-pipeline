using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions;

/// <summary>
/// Activity to retrieve a ProcessJob from the database.
/// </summary>
public sealed class GetJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetJob> _logger;

    public GetJob(
        IApplicationDbContext context,
        ILogger<GetJob> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(GetJob))]
    public async Task<ProcessJobModel?> RunAsync(
        [ActivityTrigger] Guid jobId,
        CancellationToken cancellationToken = default)
    {
        ProcessJob? job = await _context.ProcessJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found", jobId);
        }

        return job?.ToModel();
    }
}
