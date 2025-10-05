---
id: dotnet-core
title: .NET Core Style Guide
framework: dotnet
scope: general
version: 1.0.0
priority: must
appliesTo: ["**/*.cs", "**/*.csproj"]
---

# .NET Core Style Guide

## General Principles

- Target .NET 8.0 or later
- Enable nullable reference types in all projects
- Use modern C# language features (C# 12+)
- Follow [.NET Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- Prefer composition over inheritance
- Apply SOLID principles

## Project Structure

### Standard .NET Solution Structure

```
src/
├── Domain/              # Domain models, interfaces, and business logic
├── Application/         # Application services, DTOs, validators
├── Infrastructure/      # Data access, external services, implementations
├── API/                # Web API controllers and startup
└── Common/             # Shared utilities and extensions

tests/
├── Domain.Tests/       # Domain unit tests
├── Application.Tests/  # Application unit tests
├── Infrastructure.Tests/ # Infrastructure integration tests
└── API.Tests/          # API integration tests
```

### Project File Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <!-- Enable analyzers -->
  <PropertyGroup>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

</Project>
```

## Dependency Injection

### Service Registration

- Use `IServiceCollection` extension methods for clean registration
- Group related services together
- Register services in order: infrastructure, application, presentation
- Use appropriate lifetimes: Singleton, Scoped, Transient

```csharp
// Infrastructure layer - ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext - Scoped
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositories - Scoped (tied to DbContext lifetime)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        // External services - Singleton
        services.AddSingleton<IEmailService, EmailService>();
        services.AddHttpClient<IWeatherService, WeatherService>();

        return services;
    }
}

// Application layer - ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application services - Scoped
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOrderService, OrderService>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

        // MediatR
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<CreateUserCommand>());

        return services;
    }
}

// Program.cs
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
```

### Service Lifetimes

- **Transient**: Created each time they're requested (stateless services)
- **Scoped**: Created once per request (DbContext, repositories)
- **Singleton**: Created once for the application lifetime (caches, configuration)

```csharp
// Transient - new instance every time
services.AddTransient<IGuidGenerator, GuidGenerator>();

// Scoped - one instance per HTTP request
services.AddScoped<ICurrentUserService, CurrentUserService>();

// Singleton - one instance for application lifetime
services.AddSingleton<IMemoryCache, MemoryCache>();
services.AddSingleton<IConfiguration>(configuration);
```

### Keyed Services (.NET 8+)

```csharp
// Register keyed services
services.AddKeyedSingleton<INotificationService, EmailNotificationService>("email");
services.AddKeyedSingleton<INotificationService, SmsNotificationService>("sms");

// Inject keyed service
public class NotificationController : ControllerBase
{
    private readonly INotificationService _emailService;

    public NotificationController(
        [FromKeyedServices("email")] INotificationService emailService)
    {
        _emailService = emailService;
    }
}
```

## Configuration

### appsettings.json Structure

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;"
  },
  "Email": {
    "SmtpServer": "smtp.example.com",
    "SmtpPort": 587,
    "FromAddress": "noreply@example.com"
  },
  "Features": {
    "EnableNewCheckout": true,
    "MaxUploadSizeMb": 10
  }
}
```

### Options Pattern

- Use strongly-typed configuration with the Options pattern
- Validate options at startup
- Use `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>`

```csharp
// Configuration class
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public required string SmtpServer { get; init; }
    public required int SmtpPort { get; init; }
    public required string FromAddress { get; init; }
}

// Validation
public class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SmtpServer))
            return ValidateOptionsResult.Fail("SMTP server is required");

        if (options.SmtpPort <= 0)
            return ValidateOptionsResult.Fail("SMTP port must be positive");

        if (string.IsNullOrWhiteSpace(options.FromAddress))
            return ValidateOptionsResult.Fail("From address is required");

        return ValidateOptionsResult.Success;
    }
}

// Registration in Program.cs
builder.Services.AddOptions<EmailOptions>()
    .BindConfiguration(EmailOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

// Usage
public class EmailService
{
    private readonly EmailOptions _options;

    public EmailService(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // Use _options.SmtpServer, _options.SmtpPort, etc.
    }
}
```

### Secret Management

- **Never** commit secrets to source control
- Use User Secrets for local development
- Use Azure Key Vault, AWS Secrets Manager, or environment variables in production

```bash
# Initialize user secrets
dotnet user-secrets init

# Set a secret
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=..."

# List secrets
dotnet user-secrets list
```

```csharp
// Access secrets same as appsettings.json
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }
}
```

## Logging

### Structured Logging

- Use `ILogger<T>` from Microsoft.Extensions.Logging
- Use structured logging with named parameters
- Define log messages as static partial methods (C# 10+)
- Use appropriate log levels

```csharp
public partial class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public async Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        LogGettingUser(userId);

        try
        {
            User? user = await _repository.GetByIdAsync(userId, cancellationToken);

            if (user is null)
            {
                LogUserNotFound(userId);
                return null;
            }

            LogUserRetrieved(userId, user.Name);
            return user;
        }
        catch (Exception ex)
        {
            LogErrorGettingUser(ex, userId);
            throw;
        }
    }

    // Source-generated logging (high performance)
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Getting user with ID {UserId}")]
    private partial void LogGettingUser(int userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "User with ID {UserId} not found")]
    private partial void LogUserNotFound(int userId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Retrieved user {UserId}: {UserName}")]
    private partial void LogUserRetrieved(int userId, string userName);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Error getting user with ID {UserId}")]
    private partial void LogErrorGettingUser(Exception exception, int userId);
}
```

### Log Levels

- **Trace**: Very detailed diagnostic information (e.g., entering/exiting methods)
- **Debug**: Debugging information useful during development
- **Information**: General information about application flow
- **Warning**: Unexpected but recoverable situations
- **Error**: Errors that prevent completion of current operation
- **Critical**: Critical failures requiring immediate attention

## Data Access

### Entity Framework Core

- Use DbContext with proper scope management
- Always use async methods (`ToListAsync`, `FirstOrDefaultAsync`, etc.)
- Use `AsNoTracking()` for read-only queries
- Configure entities using Fluent API in `OnModelCreating`

```csharp
// DbContext
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

// Entity configuration
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasMany(u => u.Orders)
            .WithOne(o => o.User)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// Repository pattern
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<List<User>> GetActiveUsersAsync(CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }
}
```

### Dapper (Micro-ORM)

- Use Dapper for performance-critical queries
- Use parameterized queries to prevent SQL injection
- Map to strongly-typed objects

```csharp
public class UserRepository
{
    private readonly IDbConnection _connection;

    public UserRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM Users WHERE Id = @Id";
        return await _connection.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT * FROM Users
            WHERE IsActive = 1
            ORDER BY Name";

        return await _connection.QueryAsync<User>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
```

## HTTP Client

### HttpClient Best Practices

- Use `IHttpClientFactory` - never create HttpClient directly
- Use typed clients for better testability
- Use Polly for resilience (retry, circuit breaker)

```csharp
// Typed client
public class WeatherApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherApiClient> _logger;

    public WeatherApiClient(HttpClient httpClient, ILogger<WeatherApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WeatherForecast?> GetForecastAsync(
        string city,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"forecast?city={Uri.EscapeDataString(city)}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<WeatherForecast>(
                cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error fetching weather for city {City}", city);
            return null;
        }
    }
}

// Registration with Polly
builder.Services.AddHttpClient<WeatherApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddTransientHttpErrorPolicy(policy =>
    policy.WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
.AddTransientHttpErrorPolicy(policy =>
    policy.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

## Validation

### FluentValidation

- Use FluentValidation for complex validation logic
- Keep validators focused and reusable
- Register validators with DI

```csharp
// Validator
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Age)
            .GreaterThanOrEqualTo(18).WithMessage("Must be 18 or older")
            .LessThan(150);

        When(x => !string.IsNullOrEmpty(x.PhoneNumber), () =>
        {
            RuleFor(x => x.PhoneNumber)
                .Matches(@"^\+?[1-9]\d{1,14}$")
                .WithMessage("Invalid phone number format");
        });
    }
}

// Registration
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

// Usage in service
public class UserService
{
    private readonly IValidator<CreateUserRequest> _validator;

    public async Task<Result<User>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        ValidationResult validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<User>.Failure(validationResult.Errors);
        }

        // Create user
    }
}
```

## Background Services

### Hosted Services

- Use `IHostedService` or `BackgroundService` for background tasks
- Use `IHostApplicationLifetime` for graceful shutdown
- Process work in batches with delays

```csharp
public class EmailProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailProcessorService> _logger;

    public EmailProcessorService(
        IServiceProvider serviceProvider,
        ILogger<EmailProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Processor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEmailsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing emails");
            }

            // Wait before next batch
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Email Processor Service stopped");
    }

    private async Task ProcessPendingEmailsAsync(CancellationToken cancellationToken)
    {
        // Create scope for scoped services
        using IServiceScope scope = _serviceProvider.CreateScope();
        IEmailService emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        IEnumerable<Email> emails = await emailService.GetPendingEmailsAsync(100, cancellationToken);

        foreach (Email email in emails)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await emailService.SendAsync(email, cancellationToken);
        }
    }
}

// Registration
builder.Services.AddHostedService<EmailProcessorService>();
```

## Result Pattern

### Result Type for Error Handling

- Use Result pattern instead of exceptions for expected failures
- Provides better error handling and explicit error types

```csharp
// Result type
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}

// Usage
public async Task<Result<User>> CreateUserAsync(
    CreateUserRequest request,
    CancellationToken cancellationToken)
{
    // Validation
    if (string.IsNullOrWhiteSpace(request.Email))
        return Result<User>.Failure("Email is required");

    // Check for duplicate
    User? existing = await _repository.GetByEmailAsync(request.Email, cancellationToken);
    if (existing is not null)
        return Result<User>.Failure("User with this email already exists");

    // Create user
    User user = new User
    {
        Name = request.Name,
        Email = request.Email
    };

    await _repository.AddAsync(user, cancellationToken);
    return Result<User>.Success(user);
}

// Controller usage
[HttpPost]
public async Task<IActionResult> CreateUser(
    CreateUserRequest request,
    CancellationToken cancellationToken)
{
    Result<User> result = await _userService.CreateUserAsync(request, cancellationToken);

    return result.Match<IActionResult>(
        onSuccess: user => CreatedAtAction(nameof(GetUser), new { id = user.Id }, user),
        onFailure: error => BadRequest(new { Error = error }));
}
```

## Testing

### Unit Tests with xUnit

- Use xUnit for unit testing
- Follow AAA pattern: Arrange, Act, Assert
- Use descriptive test names
- Use test data attributes for multiple test cases

```csharp
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _repositoryMock;
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly UserService _sut; // System Under Test

    public UserServiceTests()
    {
        _repositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<UserService>>();
        _sut = new UserService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetUserAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        int userId = 1;
        User expectedUser = new User { Id = userId, Name = "John Doe" };
        _repositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUser);

        // Act
        User? result = await _sut.GetUserAsync(userId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedUser.Id, result.Id);
        Assert.Equal(expectedUser.Name, result.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetUserAsync_WithInvalidId_ThrowsArgumentException(int invalidId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.GetUserAsync(invalidId, CancellationToken.None));
    }
}
```

## Common Utilities

### Extension Methods

- Create extension methods for common operations
- Place in separate static classes by domain
- Use clear, descriptive names

```csharp
// String extensions
public static class StringExtensions
{
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
        => string.IsNullOrWhiteSpace(value);

    public static string Truncate(this string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

// Enumerable extensions
public static class EnumerableExtensions
{
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? source)
        => source is null || !source.Any();

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
        => source.Where(item => item is not null)!;
}

// DateTime extensions
public static class DateTimeExtensions
{
    public static bool IsBetween(this DateTime date, DateTime start, DateTime end)
        => date >= start && date <= end;

    public static DateTime StartOfDay(this DateTime date)
        => date.Date;

    public static DateTime EndOfDay(this DateTime date)
        => date.Date.AddDays(1).AddTicks(-1);
}
```
