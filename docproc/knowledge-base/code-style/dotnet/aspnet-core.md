---
id: aspnet-core
title: ASP.NET Core Style Guide
framework: aspnet-core
scope: web-api
version: 1.0.0
priority: must
appliesTo: ["**/Controllers/**/*.cs", "**/Program.cs", "**/Startup.cs"]
---
### ASP.NET Core General Principles

- Target ASP.NET Core 8.0 or later
- Use minimal APIs for simple endpoints, controllers for complex APIs
- Follow REST principles for HTTP APIs
- Use proper HTTP status codes and response formats
- Implement versioning from the start
- Use OpenAPI/Swagger for API documentation

### Program.cs and Application Startup

#### Modern Minimal Hosting Model

- Use top-level statements in Program.cs
- Configure services in builder phase
- Configure middleware in app phase
- Order middleware carefully

```csharp
using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add custom services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Build the app
WebApplication app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
```

### Controllers

#### Controller Structure

- Inherit from `ControllerBase` for APIs (not `Controller`)
- Use `[ApiController]` attribute for automatic model validation
- Use route attributes on controller and actions
- Group related endpoints in same controller

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found.</returns>
    /// <response code="200">Returns the user.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(
        int id,
        CancellationToken cancellationToken)
    {
        UserDto? user = await _userService.GetUserAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="request">The user creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user.</returns>
    /// <response code="201">User created successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        Result<UserDto> result = await _userService.CreateUserAsync(request, cancellationToken);

        return result.Match<ActionResult<UserDto>>(
            onSuccess: user => CreatedAtAction(
                nameof(GetUser),
                new { id = user.Id },
                user),
            onFailure: error => BadRequest(new { Error = error }));
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        Result<UserDto> result = await _userService.UpdateUserAsync(id, request, cancellationToken);

        return result.Match<ActionResult<UserDto>>(
            onSuccess: user => Ok(user),
            onFailure: error => error switch
            {
                "User not found" => NotFound(),
                _ => BadRequest(new { Error = error })
            });
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        bool deleted = await _userService.DeleteUserAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Searches for users.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserDto>>> SearchUsers(
        [FromQuery] UserSearchRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<UserDto> result = await _userService.SearchUsersAsync(request, cancellationToken);
        return Ok(result);
    }
}
```

#### Action Return Types

- Use `ActionResult<T>` for typed responses
- Use `IActionResult` when returning multiple types
- Use appropriate HTTP status codes

```csharp
// Typed response
public async Task<ActionResult<UserDto>> GetUser(int id)
{
    UserDto? user = await _service.GetUserAsync(id);
    return user is not null ? Ok(user) : NotFound();
}

// Multiple response types
public async Task<IActionResult> ProcessUser(int id)
{
    ProcessResult result = await _service.ProcessAsync(id);
    return result switch
    {
        Success => NoContent(),
        NotFound => NotFound(),
        Invalid => BadRequest(),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
```

### Minimal APIs

#### When to Use Minimal APIs

- Simple CRUD endpoints
- Microservices with few endpoints
- When controller overhead is unnecessary

```csharp
// Minimal API endpoints
WebApplication app = builder.Build();

RouteGroupBuilder users = app.MapGroup("/api/v1/users")
    .WithTags("Users")
    .WithOpenApi();

users.MapGet("/{id:int}", async (
    int id,
    IUserService userService,
    CancellationToken cancellationToken) =>
{
    UserDto? user = await userService.GetUserAsync(id, cancellationToken);
    return user is not null ? Results.Ok(user) : Results.NotFound();
})
.WithName("GetUser")
.Produces<UserDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

users.MapPost("/", async (
    CreateUserRequest request,
    IUserService userService,
    IValidator<CreateUserRequest> validator,
    CancellationToken cancellationToken) =>
{
    ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    Result<UserDto> result = await userService.CreateUserAsync(request, cancellationToken);
    return result.Match(
        onSuccess: user => Results.Created($"/api/v1/users/{user.Id}", user),
        onFailure: error => Results.BadRequest(new { Error = error }));
})
.WithName("CreateUser")
.Produces<UserDto>(StatusCodes.Status201Created)
.Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

users.MapDelete("/{id:int}", async (
    int id,
    IUserService userService,
    CancellationToken cancellationToken) =>
{
    bool deleted = await userService.DeleteUserAsync(id, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteUser")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);
```

### Request/Response Models

#### DTOs and Request Models

- Keep DTOs immutable with `init` or `record`
- Use validation attributes or FluentValidation
- Separate request and response models

```csharp
// Request models
public sealed record CreateUserRequest
{
    required public string Name { get; init; }
    required public string Email { get; init; }
    public string? PhoneNumber { get; init; }
    public DateOnly? BirthDate { get; init; }
}

public sealed record UpdateUserRequest
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
}

public sealed record UserSearchRequest
{
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// Response models
public sealed record UserDto(
    int Id,
    string Name,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAt);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

### Model Validation

#### Automatic Model Validation

- Use `[ApiController]` for automatic validation
- Return `ValidationProblemDetails` for validation errors
- Use data annotations or FluentValidation

```csharp
// Data annotations
public sealed record CreateUserRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(256)]
    public required string Email { get; init; }

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; init; }

    [Range(18, 150, ErrorMessage = "Age must be between 18 and 150")]
    public int? Age { get; init; }
}

// Custom validation filter
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            Dictionary<string, string[]> errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            context.Result = new BadRequestObjectResult(new
            {
                Message = "Validation failed",
                Errors = errors
            });
            return;
        }

        await next();
    }
}
```

### Error Handling in ASP.NET Core

#### Global Exception Handler

- Use exception handler middleware
- Return consistent error responses
- Log errors with proper context

```csharp
// Exception handler middleware
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int statusCode, string title, string detail) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                validationEx.Message),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Not Found",
                notFoundEx.Message),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "You are not authorized to access this resource"),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred")
        };

        _logger.LogError(
            exception,
            "Exception occurred: {Message}",
            exception.Message);

        ProblemDetails problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

// Register in Program.cs
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Use in middleware pipeline
app.UseExceptionHandler();
```

### API Versioning

#### URL-Based Versioning

```csharp
// Install package: Asp.Versioning.Http

// Registration
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Controller versioning
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class UsersV1Controller : ControllerBase
{
    [HttpGet]
    public IActionResult GetUsers() => Ok(new[] { "v1" });
}

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
public class UsersV2Controller : ControllerBase
{
    [HttpGet]
    public IActionResult GetUsers() => Ok(new[] { "v2" });
}
```

### Authentication and Authorization

#### JWT Bearer Authentication

```csharp
// Configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("UserOrAdmin", policy =>
        policy.RequireRole("User", "Admin"));
});

// Controller usage
[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Requires authentication for all endpoints
public class UsersController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous] // Override for public endpoint
    public IActionResult GetPublicUsers() => Ok();

    [HttpPost]
    [Authorize(Roles = "Admin")] // Specific role required
    public IActionResult CreateUser() => Ok();

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")] // Policy-based authorization
    public IActionResult DeleteUser(int id) => NoContent();
}
```

### CORS Configuration

```csharp
// Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(
                "https://example.com",
                "https://app.example.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    // Development policy
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Apply policy
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("AllowSpecificOrigins");
}
```

### Response Caching and Compression

#### Output Caching (.NET 8+)

```csharp
// Configuration
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Cache());

    options.AddPolicy("UserCache", builder =>
        builder.Cache()
            .Expire(TimeSpan.FromMinutes(5))
            .SetVaryByQuery("page", "pageSize")
            .Tag("users"));
});

app.UseOutputCache();

// Usage
[HttpGet]
[OutputCache(PolicyName = "UserCache")]
public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    PagedResult<UserDto> users = await _service.GetUsersAsync(page, pageSize);
    return Ok(users);
}

// Invalidate cache by tag
[HttpPost]
public async Task<ActionResult<UserDto>> CreateUser(
    [FromBody] CreateUserRequest request,
    IOutputCacheStore cache)
{
    UserDto user = await _service.CreateUserAsync(request);
    await cache.EvictByTagAsync("users", default);
    return Created($"/api/v1/users/{user.Id}", user);
}
```

#### Response Compression

```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

app.UseResponseCompression();
```

### Health Checks

```csharp
// Configuration
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddUrlGroup(
        new Uri("https://api.external.com/health"),
        name: "External API",
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<CustomHealthCheck>("Custom Check");

// Custom health check
public class CustomHealthCheck : IHealthCheck
{
    private readonly IEmailService _emailService;

    public CustomHealthCheck(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool isHealthy = await _emailService.CheckConnectionAsync(cancellationToken);

            return isHealthy
                ? HealthCheckResult.Healthy("Email service is operational")
                : HealthCheckResult.Degraded("Email service is slow");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Email service is unavailable", ex);
        }
    }
}

// Map endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        object response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");
```

### Rate Limiting (.NET 8+)

```csharp
// Configuration
builder.Services.AddRateLimiter(options =>
{
    // Fixed window limiter
    options.AddFixedWindowLimiter("fixed", options =>
    {
        options.PermitLimit = 100;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 10;
    });

    // Sliding window limiter
    options.AddSlidingWindowLimiter("sliding", options =>
    {
        options.PermitLimit = 100;
        options.Window = TimeSpan.FromMinutes(1);
        options.SegmentsPerWindow = 6;
    });

    // Token bucket limiter
    options.AddTokenBucketLimiter("token", options =>
    {
        options.TokenLimit = 100;
        options.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        options.TokensPerPeriod = 10;
    });
});

app.UseRateLimiter();

// Usage
[HttpGet]
[EnableRateLimiting("fixed")]
public IActionResult Get() => Ok();

// Disable rate limiting for specific endpoint
[HttpGet("unlimited")]
[DisableRateLimiting]
public IActionResult GetUnlimited() => Ok();
```

### OpenAPI/Swagger Configuration

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "My API",
        Version = "v1",
        Description = "API documentation",
        Contact = new OpenApiContact
        {
            Name = "Support",
            Email = "support@example.com"
        }
    });

    // Include XML comments
    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    // JWT authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
```
