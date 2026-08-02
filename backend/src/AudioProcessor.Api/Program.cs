using System;
using System.IO;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Infrastructure.Persistence;
using AudioProcessor.Infrastructure.Persistence.Repositories;
using AudioProcessor.Infrastructure.Storage;
using AudioProcessor.Infrastructure.Queue;
using AudioProcessor.Infrastructure.Services;
using AudioProcessor.Infrastructure.ExternalServices;
using Microsoft.Extensions.Options;
using AudioProcessor.Api.BackgroundServices;
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
builder.Services.AddScoped<IAudioRepository, AudioRepository>();
builder.Services.AddScoped<IAudioStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IAudioCompressionService, FFmpegAudioCompressionService>();
builder.Services.Configure<LocalModelWorkerOptions>(builder.Configuration.GetSection(LocalModelWorkerOptions.SectionName));
builder.Services.AddSingleton<IAudioSummaryService, LocalWhisperCppSummaryService>();
builder.Services.AddScoped<UploadAudioUseCase>();
builder.Services.AddScoped<ProcessAudioCompressionCommandHandler>();
builder.Services.AddScoped<ProcessAudioSummaryCommandHandler>();

// Register Singleton Background Queue
builder.Services.AddSingleton<IBackgroundJobQueue, ChannelBackgroundJobQueue>();

// Register Background Worker
builder.Services.AddHostedService<AudioProcessingBackgroundService>();

// Register HttpClient for Use Case
builder.Services.AddHttpClient();

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

app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "Audio Processor API is running!");

// Automatically apply pending EF Core migrations in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

app.Run();
