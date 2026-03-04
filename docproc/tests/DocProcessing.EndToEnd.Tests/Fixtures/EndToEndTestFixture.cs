using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline;
using DocProcessing.Application.Services;
using DocProcessing.Application.Services.OCR;
using DocProcessing.Application.Services.Preprocessing;
using DocProcessing.EndToEnd.Tests.Builders;
using DocProcessing.EndToEnd.Tests.Helpers;
using DocProcessing.EndToEnd.Tests.Mocks;
using DocProcessing.Infrastructure.Factories;
using DocProcessing.TestUtilities.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DocProcessing.EndToEnd.Tests.Fixtures;

/// <summary>
/// Wires real application services with mocked infrastructure for E2E tests.
/// Each test creates its own instance for full isolation.
/// </summary>
public sealed class EndToEndTestFixture : IDisposable
{
    public ServiceProvider ServiceProvider { get; }
    public InMemoryDbContext DbContext { get; }
    public InMemoryStorageService StorageService { get; }
    public Mock<IOcrService> OcrServiceMock { get; }
    public Mock<IMessagingService> MessagingServiceMock { get; }
    public FakeTimeProvider TimeProvider { get; }
    public ControllableActivityFactory ActivityFactory { get; }

    public IDocumentService DocumentService => ServiceProvider.GetRequiredService<IDocumentService>();
    public IProcessJobService ProcessJobService => ServiceProvider.GetRequiredService<IProcessJobService>();

    public EndToEndTestFixture()
    {
        DbContext = new InMemoryDbContext();
        StorageService = new InMemoryStorageService();
        OcrServiceMock = new Mock<IOcrService>();
        MessagingServiceMock = new Mock<IMessagingService>();
        TimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 4, 10, 0, 0, System.TimeSpan.Zero));

        var services = new ServiceCollection();

        // Database
        services.AddSingleton<IApplicationDbContext>(DbContext);

        // Storage (stateful fake)
        services.AddSingleton<IStorageService>(StorageService);

        // External services (mocked)
        services.AddSingleton(OcrServiceMock.Object);
        services.AddSingleton(MessagingServiceMock.Object);

        // Time
        services.AddSingleton<System.TimeProvider>(TimeProvider);

        // Options
        services.AddSingleton(Options.Create(new OcrOptions
        {
            Provider = "Mock",
            OutputBlobContainer = "ocr-results",
            ModelId = "prebuilt-layout"
        }));
        services.AddSingleton(Options.Create(new PreprocessOptions
        {
            OutputBlobContainer = "preprocess-results",
            EnableUnicodeNormalization = true,
            EnableWhitespaceCleanup = true,
            ConvertTablesToStructured = true
        }));

        // Real application services
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IProcessJobService, ProcessJobService>();

        // Preprocessing services (real)
        services.AddSingleton<ITextNormalizer, TextNormalizer>();
        services.AddSingleton<ITableConverter, TableConverter>();
        services.AddSingleton<IFieldParser, FieldParser>();

        // Pipeline stage activities (real)
        services.AddSingleton<OcrStageActivity>();
        services.AddSingleton<PreprocessStageActivity>();
        services.AddSingleton<EmbedStageActivity>();
        services.AddSingleton<ExtractStageActivity>();
        services.AddSingleton<ValidateStageActivity>();
        services.AddSingleton<PersistStageActivity>();
        services.AddSingleton<NotifyStageActivity>();

        // Pipeline factory (real, wrapped with controllable)
        services.AddSingleton<PipelineActivityFactory>();

        // Logging (fake loggers)
        services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));

        ServiceProvider = services.BuildServiceProvider();

        // Wrap real factory with controllable wrapper
        var realFactory = ServiceProvider.GetRequiredService<PipelineActivityFactory>();
        ActivityFactory = new ControllableActivityFactory(realFactory);
    }

    public UploadRequestBuilder CreateUploadBuilder()
    {
        return new UploadRequestBuilder(
            StorageService,
            DocumentService,
            ProcessJobService,
            MessagingServiceMock.Object);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        ServiceProvider.Dispose();
    }
}
