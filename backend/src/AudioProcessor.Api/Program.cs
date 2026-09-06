using System;
using System.IO;
using System.Threading;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Infrastructure.Persistence;
using AudioProcessor.Infrastructure.Persistence.Repositories;
using AudioProcessor.Infrastructure.Storage;
using AudioProcessor.Infrastructure.Services;
using AudioProcessor.Infrastructure.ExternalServices;
using AudioProcessor.Infrastructure.Realtime;
using Microsoft.Extensions.Options;
using AppSupabaseOptions = AudioProcessor.Infrastructure.ExternalServices.SupabaseOptions;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Supabase;

var envFilePath = FindEnvFile();
if (envFilePath is not null)
{
    Env.Load(envFilePath);
}

static string? FindEnvFile()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        string candidate = Path.Combine(dir.FullName, "infrastructure", ".env");
        if (File.Exists(candidate))
        {
            return candidate;
        }
        dir = dir.Parent;
    }
    return null;
}

var builder = WebApplication.CreateBuilder(args);

const string frontendCorsPolicy = "FrontendDevelopment";
string frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy => policy
        .WithOrigins(frontendOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Bind Supabase options from environment variables
builder.Services.Configure<AppSupabaseOptions>(options =>
{
    options.Url = Environment.GetEnvironmentVariable("SUPABASE_URL")
        ?? throw new InvalidOperationException("SUPABASE_URL environment variable is not set.");
    options.Key = Environment.GetEnvironmentVariable("SUPABASE_KEY")
        ?? throw new InvalidOperationException("SUPABASE_KEY environment variable is not set.");
    options.BucketName = Environment.GetEnvironmentVariable("BUCKET")
        ?? throw new InvalidOperationException("BUCKET environment variable is not set.");
});

// Register Supabase client as a singleton
builder.Services.AddSingleton(provider =>
{
    string url = Environment.GetEnvironmentVariable("SUPABASE_URL")!;
    string key = Environment.GetEnvironmentVariable("SUPABASE_KEY")!;

    var supabaseClientOptions = new Supabase.SupabaseOptions
    {
        AutoConnectRealtime = false
    };

    var client = new Client(url, key, supabaseClientOptions);
    client.InitializeAsync().GetAwaiter().GetResult();
    return client;
});

// Register EF Core with PostgreSQL
string connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Application and Infrastructure services
builder.Services.AddSignalR();
builder.Services.AddScoped<IAudioStatusNotifier, SignalRAudioStatusNotifier>();
builder.Services.AddScoped<IAudioRepository, AudioRepository>();
builder.Services.AddScoped<IAudioStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IAudioCompressionService, FFmpegAudioCompressionService>();
builder.Services.AddScoped<IAudioNoiseReductionService, FFmpegAudioNoiseReductionService>();
builder.Services.Configure<LocalModelWorkerOptions>(builder.Configuration.GetSection(LocalModelWorkerOptions.SectionName));
builder.Services.AddScoped<IAudioTranscriptionService, LocalWhisperCppTranscriptionService>();
builder.Services.AddScoped<IAudioSummarizationService, LocalLlamaCppSummarizationService>();
builder.Services.AddScoped<UploadAudioUseCase>();
builder.Services.AddScoped<ProcessAudioCompressionCommandHandler>();
builder.Services.AddScoped<ProcessAudioSummaryCommandHandler>();

// Single shared semaphore limiting concurrent heavy external processes (FFmpeg, STT, LLM).
// Read directly from an environment variable (not appsettings) so benchmark runs can
// change the limit by restarting the container, without a rebuild.
builder.Services.AddSingleton(provider =>
{
    string? rawLimit = Environment.GetEnvironmentVariable("EXTERNAL_PROCESS_SEMAPHORE_LIMIT");
    int limit = int.TryParse(rawLimit, out var parsedLimit) ? parsedLimit : 2;
    return new SemaphoreSlim(Math.Max(1, limit));
});

// Downloads the original audio once, then runs Compression and
// Transcription→Summary as independent Tasks (Task.Run + Task.WhenAll).
builder.Services.AddScoped<IAudioProcessingLauncher, AudioProcessingLauncher>();

// Register HttpClient, used by the launcher to download the original audio.
builder.Services.AddHttpClient();

// Background admission control: polls for Pending audios and processes a
// batch at a time with Parallel.ForEachAsync, bounding how many whole audios
// run concurrently at the system level (distinct from the external-process
// SemaphoreSlim above and from the per-audio Task.WhenAll in the launcher).
builder.Services.AddHostedService<AudioBackgroundProcessingService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Audio Processor API",
        Version = "v1",
        Description = "API for uploading and managing audio files with Supabase Storage and PostgreSQL."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Audio Processor API v1"));
}

app.UseCors(frontendCorsPolicy);
app.UseAuthorization();
app.MapControllers();
app.MapHub<AudioStatusHub>("/api/hubs/audio-status");
app.MapGet("/", () => "Audio Processor API is running!");

// Automatically apply pending EF Core migrations in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

app.Run();
