---
id: csharp-core
title: C# Core Style Guide
framework: csharp
scope: general
version: 1.0.0
priority: must
appliesTo: ["**/*.cs"]
---
### C# General Principles

- Target modern C# (C# 12+) and .NET 8+
- Enable nullable reference types in all projects (`<Nullable>enable</Nullable>`)
- Use explicit typing instead of `var` for better code readability and maintainability
- Prefer expression-bodied members for simple operations
- Follow Microsoft's official [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

### C# Naming Conventions

#### Pascal Case

- Classes, structs, records, interfaces: `UserAccount`, `IUserService`
- Methods, properties, events: `GetUser()`, `UserName`, `OnChanged`
- Namespaces: `MyCompany.MyProduct.MyFeature`
- Public fields (avoid when possible): `MaxValue`
- Enum types and values: `UserRole.Administrator`

#### Camel Case

- Local variables: `userName`, `isValid`
- Private fields: `_userName`, `_isValid` (with underscore prefix)
- Method parameters: `userId`, `requestData`

#### Other Conventions

- Interfaces: Prefix with `I` (e.g., `IUserService`, `IRepository<T>`)
- Type parameters: Single uppercase letter or descriptive PascalCase with `T` prefix (e.g., `T`, `TKey`, `TValue`, `TEntity`)
- Constants: PascalCase (e.g., `MaxRetryCount`, `DefaultTimeout`)
- Async methods: Suffix with `Async` (e.g., `GetUserAsync()`)

```csharp
// Good
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken);
}

public class UserRepository : IUserRepository
{
    private readonly DbContext _dbContext;
    private const int MaxRetryCount = 3;

    public async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken)
    {
        User? user = await _dbContext.Users.FindAsync(userId, cancellationToken);
        return user;
    }
}
```

### Modern C# Features

#### Nullable Reference Types

- **Always** enable nullable reference types
- Use `?` for nullable reference types
- Use `!` null-forgiving operator sparingly and only when you're certain
- Initialize non-nullable properties in constructor or with property initializers

```csharp
// Good
public class User
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Email { get; init; }  // Nullable

    public User(string name)
    {
        Name = name;
    }
}

// Bad - CS8618: Non-nullable property must contain a non-null value
public class User
{
    public string Name { get; set; }  // Missing initialization
}
```

#### Records and Init-Only Properties

- Use `record` for immutable data transfer objects (DTOs) and value objects
- Use `record class` explicitly when inheritance is needed
- Use `record struct` for small, performance-critical value types
- Prefer `init` over `set` for immutability

```csharp
// Primary constructor syntax (C# 12+)
public record User(int Id, string Name, string? Email);

// Explicit syntax with validation
public record CreateUserRequest
{
    required public string Name { get; init; }
    public string? Email { get; init; }
    public DateOnly? BirthDate { get; init; }
}

// Record with additional members
public record UserDto(int Id, string Name)
{
    public string DisplayName => $"User: {Name}";
}
```

#### Pattern Matching

- Use pattern matching for type checks and value comparisons
- Prefer `is` patterns over traditional casting
- Use switch expressions for concise transformations

```csharp
// Type patterns
if (obj is User user)
{
    Console.WriteLine(user.Name);
}

// Property patterns
if (user is { IsActive: true, Role: UserRole.Admin })
{
    // User is active admin
}

// Switch expressions
string GetStatusMessage(OrderStatus status) => status switch
{
    OrderStatus.Pending => "Your order is pending",
    OrderStatus.Processing => "Your order is being processed",
    OrderStatus.Shipped => "Your order has been shipped",
    OrderStatus.Delivered => "Your order has been delivered",
    _ => throw new ArgumentOutOfRangeException(nameof(status))
};

// List patterns (C# 11+)
int GetScore(int[] numbers) => numbers switch
{
    [] => 0,
    [var single] => single,
    [var first, var second] => first + second,
    [var first, .., var last] => first + last,
    _ => numbers.Sum()
};
```

#### Required Members (C# 11+)

- Use `required` modifier for properties that must be initialized
- Reduces need for multiple constructors

```csharp
public class CreateUserRequest
{
    required public string UserName { get; init; }
    required public string Email { get; init; }
    public string? PhoneNumber { get; init; }
}

// Usage - compiler enforces required properties
CreateUserRequest request = new CreateUserRequest
{
    UserName = "john_doe",
    Email = "john@example.com"
};
```

#### File-Scoped Namespaces (C# 10+)

- Use file-scoped namespaces to reduce indentation
- One namespace per file

```csharp
// Good - file-scoped namespace
namespace MyCompany.MyProduct.Domain;

public class User
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

// Avoid - traditional namespace (adds extra indentation)
namespace MyCompany.MyProduct.Domain
{
    public class User
    {
        // ...
    }
}
```

#### Target-Typed New (C# 9+)

- Use `new()` when type is obvious from context

```csharp
// Good
User user = new();
List<string> names = new();
Dictionary<int, User> userMap = new();

// Also good for return statements
public User CreateUser() => new() { Name = "Default" };

// Traditional syntax with explicit type
User user = new User();
```

### Classes and Structs

#### Class Structure Order

1. Constants
2. Static fields
3. Instance fields (private, then protected, then public)
4. Constructors
5. Properties
6. Events
7. Methods (public, then protected, then private)
8. Nested types

```csharp
public class UserService
{
    // Constants
    private const int MaxRetryCount = 3;

    // Static fields
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // Instance fields
    private readonly IUserRepository _repository;
    private readonly ILogger<UserService> _logger;

    // Constructor
    public UserService(IUserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // Properties
    public int TotalUsers { get; private set; }

    // Methods
    public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    private void LogUserAccess(int userId)
    {
        _logger.LogInformation("User {UserId} accessed", userId);
    }
}
```

#### Access Modifiers

- Always specify access modifiers explicitly (don't rely on defaults)
- Use most restrictive access level possible
- Prefer `private` fields with `public` properties

```csharp
// Good - explicit access modifiers
public class User
{
    private readonly int _id;
    public string Name { get; private set; } = string.Empty;

    public User(int id) => _id = id;
}
```

#### Immutability in CSharp

- Prefer immutable types when possible
- Use `readonly` fields and `init` properties
- Use `record` for immutable data structures

```csharp
// Immutable class
public sealed class UserId
{
    public int Value { get; }

    public UserId(int value)
    {
        if (value <= 0)
            throw new ArgumentException("User ID must be positive", nameof(value));
        Value = value;
    }
}

// Immutable record
public record Address(string Street, string City, string Country);
```

### Methods and Properties

#### Method Guidelines

- Keep methods short and focused (single responsibility)
- Use expression-bodied members for simple operations
- Always specify return types explicitly
- Use `async` suffix for asynchronous methods

```csharp
// Expression-bodied member
public int GetAge(DateOnly birthDate)
    => DateTime.Today.Year - birthDate.Year;

// Expression-bodied property
public string FullName => $"{FirstName} {LastName}";

// Async method
public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken)
{
    return await _repository.FindAsync(id, cancellationToken);
}
```

#### Property Guidelines

- Use auto-properties when no logic is needed
- Prefer `init` over `set` for immutability
- Use expression-bodied properties for computed values
- Never throw exceptions from property getters

```csharp
public class User
{
    // Auto-property with init
    public int Id { get; init; }

    // Auto-property with private setter
    public string Name { get; private set; } = string.Empty;

    // Computed property
    public string DisplayName => $"User: {Name}";

    // Property with validation in setter
    private int _age;
    public int Age
    {
        get => _age;
        set
        {
            if (value < 0 || value > 150)
                throw new ArgumentOutOfRangeException(nameof(value));
            _age = value;
        }
    }
}
```

### CSharpAsync/Await

#### CSharp Best Practices

- **Always** pass `CancellationToken` to async methods
- Use `ConfigureAwait(false)` in library code (not in ASP.NET Core)
- Avoid `async void` except for event handlers
- Return `Task` or `ValueTask<T>`, not `void`
- Don't mix `.Result` or `.Wait()` with async code (causes deadlocks)

```csharp
// Good - with CancellationToken
public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken)
{
    return await _dbContext.Users
        .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}

// Good - ValueTask for performance-critical paths
public async ValueTask<bool> ExistsAsync(int id, CancellationToken cancellationToken)
{
    return await _dbContext.Users.AnyAsync(u => u.Id == id, cancellationToken);
}

// Bad - no CancellationToken
public async Task<User?> GetUserAsync(int id)
{
    return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
}

// Bad - async void (except for event handlers)
public async void ProcessUser(int id) { }

// Good - event handler can be async void
private async void OnButtonClick(object sender, EventArgs e)
{
    await ProcessDataAsync();
}
```

#### Async Streams (C# 8+)

- Use `IAsyncEnumerable<T>` for streaming data

```csharp
public async IAsyncEnumerable<User> GetUsersAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (User user in _dbContext.Users.AsAsyncEnumerable()
        .WithCancellation(cancellationToken))
    {
        yield return user;
    }
}

// Usage
await foreach (User user in userService.GetUsersAsync(cancellationToken))
{
    Console.WriteLine(user.Name);
}
```

### LINQ

#### Querying Best Practices

- Prefer method syntax over query syntax for simple queries
- Use query syntax for complex queries with multiple `from` clauses
- Chain LINQ methods on separate lines for readability

```csharp
// Method syntax - preferred for simple queries
List<UserDto> activeUsers = users
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .Select(u => new UserDto(u.Id, u.Name))
    .ToList();

// Query syntax - for complex queries
IEnumerable<dynamic> userOrders =
    from user in users
    from order in orders
    where user.Id == order.UserId
    where order.Status == OrderStatus.Completed
    orderby order.CreatedAt descending
    select new { user.Name, order.Total };

// Avoid - mixing styles
IEnumerable<User> result = (from u in users select u).Where(u => u.IsActive);
```

### Exception Handling

#### Exception Best Practices

- Use specific exception types, not generic `Exception`
- Include meaningful error messages
- Use `ArgumentNullException.ThrowIfNull()` (C# 11+)
- Don't catch exceptions you can't handle
- Use when clauses in catch blocks for filtering

```csharp
// Good - specific exceptions with messages
public User GetUser(int id)
{
    if (id <= 0)
        throw new ArgumentOutOfRangeException(nameof(id), "User ID must be positive");

    User? user = _repository.Find(id);
    if (user is null)
        throw new UserNotFoundException($"User with ID {id} not found");

    return user;
}

// Good - ArgumentNullException.ThrowIfNull (C# 11+)
public void ProcessUser(User user)
{
    ArgumentNullException.ThrowIfNull(user);
    // Process user...
}

// Good - filtered catch with when clause
try
{
    await ProcessAsync();
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    _logger.LogWarning("Resource not found");
}

// Custom exception
public class UserNotFoundException : Exception
{
    public int UserId { get; }

    public UserNotFoundException(int userId)
        : base($"User with ID {userId} was not found")
    {
        UserId = userId;
    }
}
```

### String Handling

#### String Best Practices

- Use string interpolation over concatenation
- Use raw string literals (C# 11+) for multi-line strings
- Use `StringBuilder` for loops with string concatenation
- Use `string.Equals()` with `StringComparison` for comparisons

```csharp
// String interpolation
string message = $"User {user.Name} has {user.OrderCount} orders";

// Raw string literals (C# 11+)
string json = """
    {
        "name": "John Doe",
        "email": "john@example.com"
    }
    """;

// StringBuilder for loops
StringBuilder builder = new StringBuilder();
foreach (Item item in items)
{
    builder.AppendLine(item.ToString());
}
string result = builder.ToString();

// String comparison
if (name.Equals("admin", StringComparison.OrdinalIgnoreCase))
{
    // ...
}
```

### Collections

#### Collection Guidelines

- Use `List<T>` for general-purpose lists
- Use `IReadOnlyList<T>` or `ImmutableList<T>` for exposing read-only collections
- Use `Dictionary<TKey, TValue>` for key-value pairs
- Use collection expressions (C# 12+) for initialization

```csharp
// Collection expressions (C# 12+)
int[] numbers = [1, 2, 3, 4, 5];
List<string> names = ["Alice", "Bob", "Charlie"];

// Spread operator (C# 12+)
int[] allNumbers = [..numbers, 6, 7, 8];

// ImmutableList for read-only collections
public ImmutableList<string> GetTags() => _tags.ToImmutableList();

// Dictionary initialization
Dictionary<int, string> statusMap = new Dictionary<int, string>
{
    [1] = "Active",
    [2] = "Inactive",
    [3] = "Suspended"
};
```

### Generics (CSharp-specific)

#### Generic Guidelines

- Use descriptive names for generic parameters when context is unclear
- Constrain generics with `where` clauses
- Use generic math (C# 11+) for numeric operations

```csharp
// Simple generic
public class Repository<T> where T : class
{
    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        // Implementation
    }
}

// Multiple constraints
public class EntityService<TEntity, TId>
    where TEntity : class, IEntity<TId>
    where TId : struct
{
    // Implementation
}

// Generic math (C# 11+)
public static T Add<T>(T left, T right) where T : INumber<T>
    => left + right;
```

### Documentation  (CSharp-specific)

#### XML Documentation Comments

- Document all public APIs
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
- Use `<see cref=""/>` for cross-references

```csharp
/// <summary>
/// Retrieves a user by their unique identifier.
/// </summary>
/// <param name="id">The unique identifier of the user.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>
/// The <see cref="User"/> if found; otherwise, <c>null</c>.
/// </returns>
/// <exception cref="ArgumentOutOfRangeException">
/// Thrown when <paramref name="id"/> is less than or equal to zero.
/// </exception>
public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken)
{
    if (id <= 0)
        throw new ArgumentOutOfRangeException(nameof(id), "User ID must be positive");

    return await _repository.FindAsync(id, cancellationToken);
}
```

### Performance Considerations (CSharp-specific)

#### Performance Best Practices

- Use `ValueTask<T>` for hot paths that may complete synchronously
- Use `stackalloc` for small temporary buffers
- Use `Span<T>` and `Memory<T>` for memory-efficient operations
- Avoid allocations in tight loops

```csharp
// ValueTask for potentially synchronous operations
public ValueTask<User?> GetFromCacheAsync(int id)
{
    if (_cache.TryGetValue(id, out User? user))
        return new ValueTask<User?>(user);

    return new ValueTask<User?>(LoadFromDatabaseAsync(id));
}

// Span<T> for stack-allocated buffers
public static int ParseVersion(ReadOnlySpan<char> version)
{
    Span<Range> ranges = stackalloc Range[3];
    int count = version.Split(ranges, '.');
    // Parse components
}

// Avoid allocations in loops
for (int i = 0; i < items.Count; i++)  // Use for instead of foreach to avoid enumerator allocation
{
    Item item = items[i];
    // Process item
}
```

### Code Organization

#### Namespace and Using Directives

- Use file-scoped namespaces
- Place `using` directives outside namespace (C# 10+)
- Group and order usings: System namespaces first, then third-party, then local
- Use global usings for commonly used namespaces (in GlobalUsings.cs)

```csharp
// GlobalUsings.cs
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// Regular file
using Microsoft.EntityFrameworkCore;
using MyCompany.MyProduct.Domain;

namespace MyCompany.MyProduct.Infrastructure;

public class UserRepository : IUserRepository
{
    // Implementation
}
```

### Avoid Common Pitfalls

#### Anti-Patterns to Avoid

```csharp
// ❌ Avoid - mutable DTO
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// ✅ Prefer - immutable record
public record UserDto(int Id, string Name);

// ❌ Avoid - async void
public async void ProcessData() { }

// ✅ Use - async Task
public async Task ProcessDataAsync() { }

// ❌ Avoid - .Result or .Wait()
User user = GetUserAsync(id).Result;

// ✅ Use - await
User user = await GetUserAsync(id);

// ❌ Avoid - string concatenation in loops
string result = "";
foreach (Item item in items)
    result += item.ToString();

// ✅ Use - StringBuilder
StringBuilder builder = new StringBuilder();
foreach (Item item in items)
    builder.Append(item.ToString());
string result = builder.ToString();
```
