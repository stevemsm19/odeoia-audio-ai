# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and all project files for dependency resolution
COPY backend/AudioProcessor.slnx ./backend/
COPY backend/src/AudioProcessor.Domain/AudioProcessor.Domain.csproj ./backend/src/AudioProcessor.Domain/
COPY backend/src/AudioProcessor.Application/AudioProcessor.Application.csproj ./backend/src/AudioProcessor.Application/
COPY backend/src/AudioProcessor.Infrastructure/AudioProcessor.Infrastructure.csproj ./backend/src/AudioProcessor.Infrastructure/
COPY backend/src/AudioProcessor.Api/AudioProcessor.Api.csproj ./backend/src/AudioProcessor.Api/
COPY backend/tests/AudioProcessor.Tests/AudioProcessor.Tests.csproj ./backend/tests/AudioProcessor.Tests/

# Restore dependencies (separate layer for caching)
RUN dotnet restore backend/AudioProcessor.slnx

# Copy remaining source code
COPY backend/ ./backend/

# Publish (restore is already done, but we allow it to reconcile analyzers properly)
RUN dotnet publish backend/src/AudioProcessor.Api/AudioProcessor.Api.csproj \
    -c Release \
    -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AudioProcessor.Api.dll"]
