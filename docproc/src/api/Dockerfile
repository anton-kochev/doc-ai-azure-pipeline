# ======================================
# Base image for running the Function App
# ======================================
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

# ======================================
# Build stage
# ======================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the csproj file first (for build cache)
COPY ["docproc/src/DocProcessing.Api/DocProcessing.Api.csproj", "api/"]
RUN dotnet restore "api/DocProcessing.Api.csproj" --use-current-runtime

# Copy everything else
COPY . .

# Build and publish in one layer to reduce intermediate data
WORKDIR "docproc/src/DocProcessing.Api/DocProcessing.Api.csproj"
RUN dotnet publish "DocProcessing.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:SelfContained=false

# ======================================
# Final image
# ======================================
FROM base AS final
WORKDIR /home/site/wwwroot

# Copy published output from build stage
COPY --from=build /app/publish .
