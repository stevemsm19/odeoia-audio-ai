# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and all project files for dependency resolution
COPY backend/AudioProcessor.sln ./backend/
COPY backend/src/AudioProcessor.Domain/AudioProcessor.Domain.csproj ./backend/src/AudioProcessor.Domain/
COPY backend/src/AudioProcessor.Application/AudioProcessor.Application.csproj ./backend/src/AudioProcessor.Application/
COPY backend/src/AudioProcessor.Infrastructure/AudioProcessor.Infrastructure.csproj ./backend/src/AudioProcessor.Infrastructure/
COPY backend/src/AudioProcessor.Api/AudioProcessor.Api.csproj ./backend/src/AudioProcessor.Api/
COPY backend/tests/AudioProcessor.Tests/AudioProcessor.Tests.csproj ./backend/tests/AudioProcessor.Tests/

# Restore dependencies (separate layer for caching)
RUN dotnet restore backend/AudioProcessor.sln

# Copy remaining source code
COPY backend/ ./backend/

# Publish (restore is already done, but we allow it to reconcile analyzers properly)
RUN dotnet publish backend/src/AudioProcessor.Api/AudioProcessor.Api.csproj \
    -c Release \
    -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install the CPU-only local inference toolchains. Models are intentionally not
# downloaded at build time; mount them into /models to keep images small and
# deployments reproducible.
RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg python3 git cmake build-essential && \
    rm -rf /var/lib/apt/lists/*

# Build only the two command-line targets needed at runtime. `-j1` avoids a
# memory spike on Docker Desktop and separate layers expose the failing project.
RUN git clone --depth 1 https://github.com/ggml-org/whisper.cpp.git /opt/whisper.cpp && \
    cmake -S /opt/whisper.cpp -B /opt/whisper.cpp/build \
      -DCMAKE_BUILD_TYPE=Release -DWHISPER_BUILD_TESTS=OFF -DGGML_NATIVE=OFF -DGGML_CPU_ALL_VARIANTS=OFF && \
    cmake --build /opt/whisper.cpp/build --target whisper-cli --config Release -j1 && \
    ln -s /opt/whisper.cpp/build/bin/whisper-cli /usr/local/bin/whisper-cli

RUN git clone --depth 1 https://github.com/ggml-org/llama.cpp.git /opt/llama.cpp && \
    cmake -S /opt/llama.cpp -B /opt/llama.cpp/build \
      -DCMAKE_BUILD_TYPE=Release -DLLAMA_BUILD_TESTS=OFF -DGGML_NATIVE=OFF -DGGML_CPU_ALL_VARIANTS=OFF && \
    cmake --build /opt/llama.cpp/build --target llama-cli --config Release -j1 && \
    ln -s /opt/llama.cpp/build/bin/llama-cli /usr/local/bin/llama-cli

COPY --from=build /app/publish .
COPY workers/ /app/workers/

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "AudioProcessor.Api.dll"]
