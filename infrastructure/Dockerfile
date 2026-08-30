# syntax=docker/dockerfile:1

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

# Models are not baked into the image; mount them into /models at runtime.
RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg python3 git cmake build-essential && \
    rm -rf /var/lib/apt/lists/*

# -j4: balance between build speed and avoiding OOM on constrained hosts.
# The cache mount doesn't survive into the image layer, so bin/ (the CLI plus
# its shared libs) is copied out to a persistent path before it's torn down.
RUN --mount=type=cache,target=/opt/whisper.cpp-build,id=whisper-cpp-build \
    git clone --depth 1 https://github.com/ggml-org/whisper.cpp.git /opt/whisper.cpp && \
    cmake -S /opt/whisper.cpp -B /opt/whisper.cpp-build \
      -DCMAKE_BUILD_TYPE=Release -DWHISPER_BUILD_TESTS=OFF -DGGML_NATIVE=OFF -DGGML_CPU_ALL_VARIANTS=OFF -DGGML_OPENMP=OFF && \
    cmake --build /opt/whisper.cpp-build --target whisper-cli --config Release -j4 && \
    mkdir -p /usr/local/lib/whisper-cpp && \
    cp -a /opt/whisper.cpp-build/bin/. /usr/local/lib/whisper-cpp/ && \
    ln -s /usr/local/lib/whisper-cpp/whisper-cli /usr/local/bin/whisper-cli

# Binaries' baked-in RPATH points at the torn-down build path, so the linker
# needs LD_LIBRARY_PATH to find the copies above instead.
ENV LD_LIBRARY_PATH=/usr/local/lib/whisper-cpp:/usr/local/lib/llama-cpp

# llama-cli unavoidably links tools/mtmd here; LLAMA_BUILD_MTMD doesn't gate it.
RUN --mount=type=cache,target=/opt/llama.cpp-build,id=llama-cpp-build \
    git clone --depth 1 https://github.com/ggml-org/llama.cpp.git /opt/llama.cpp && \
    cmake -S /opt/llama.cpp -B /opt/llama.cpp-build \
      -DCMAKE_BUILD_TYPE=Release -DLLAMA_BUILD_TESTS=OFF -DGGML_NATIVE=OFF -DGGML_CPU_ALL_VARIANTS=OFF -DGGML_OPENMP=OFF && \
    cmake --build /opt/llama.cpp-build --target llama-cli --config Release -j4 && \
    mkdir -p /usr/local/lib/llama-cpp && \
    cp -a /opt/llama.cpp-build/bin/. /usr/local/lib/llama-cpp/ && \
    ln -s /usr/local/lib/llama-cpp/llama-cli /usr/local/bin/llama-cli

COPY --from=build /app/publish .
COPY workers/ /app/workers/

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "AudioProcessor.Api.dll"]
