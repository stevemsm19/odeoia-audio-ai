using System;
using System.IO;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Infrastructure.Persistence;
using AudioProcessor.Infrastructure.Persistence.Repositories;
using AudioProcessor.Infrastructure.Storage;
using AudioProcessor.Infrastructure.Services;
using AudioProcessor.Infrastructure.ExternalServices;
using AppSupabaseOptions = AudioProcessor.Infrastructure.ExternalServices.SupabaseOptions;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        .AllowAnyMethod());
});

builder.Services.Configure<AppSupabaseOptions>(options =>
{
    options.Url = Environment.GetEnvironmentVariable("SUPABASE_URL")
        ?? throw new InvalidOperationException("SUPABASE_URL environment variable is not set.");
    options.Key = Environment.GetEnvironmentVariable("SUPABASE_KEY")
        ?? throw new InvalidOperationException("SUPABASE_KEY environment variable is not set.");
    options.BucketName = Environment.GetEnvironmentVariable("BUCKET")
        ?? throw new InvalidOperationException("BUCKET environment variable is not set.");
});

string connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IAudioRepository, AudioRepository>();
builder.Services.AddScoped<IAudioStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IAudioCompressionService, FFmpegAudioCompressionService>();
builder.Services.Configure<LocalModelWorkerOptions>(builder.Configuration.GetSection(LocalModelWorkerOptions.SectionName));
builder.Services.AddScoped<IAudioTranscriptionService, LocalWhisperCppTranscriptionService>();
builder.Services.AddScoped<IAudioSummarizationService, LocalLlamaCppSummarizationService>();
builder.Services.AddScoped<UploadAudioUseCase>();
builder.Services.AddScoped<ProcessAudioCompressionCommandHandler>();
builder.Services.AddScoped<ProcessAudioSummaryCommandHandler>();

builder.Services.AddScoped<IAudioProcessingLauncher, AudioProcessingLauncher>();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<AudioBackgroundProcessingThread>();

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
app.MapGet("/", () => "Audio Processor API is running!");

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
}

app.Services.GetRequiredService<AudioBackgroundProcessingThread>().Start();

app.Run();
