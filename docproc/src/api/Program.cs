using Api.Configuration;
using Api.Endpoints;
using Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.Configure<AzureStorageOptions>(
    builder.Configuration.GetSection(AzureStorageOptions.SectionName));

builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Configure OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map endpoints
app.MapGet("/", () => "Hello World!");
app.MapUploadEndpoints();

app.Run();
