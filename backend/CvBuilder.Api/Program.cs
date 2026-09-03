using System.Text.Json.Serialization;
using CvBuilder.Api.Ai;
using CvBuilder.Api.Api;
using QuestPDF.Infrastructure;

// QuestPDF is free under the Community licence for individuals and small companies.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// No database, no session, no cache: the browser holds the CV and posts it with each
// request. That keeps hosting to a single stateless container.

// The DeepSeek key stays server-side. Set it with DeepSeek__ApiKey; the app runs
// fine without one, and the tailoring endpoint says so if it is missing.
var deepSeek = builder.Configuration.GetSection("DeepSeek").Get<DeepSeekOptions>() ?? new DeepSeekOptions();
builder.Services.AddSingleton(deepSeek);
builder.Services.AddHttpClient<DeepSeekClient>(c => c.Timeout = TimeSpan.FromSeconds(120));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    // SectionKind travels as "Timeline"/"Grouped"/… rather than 0/1/2.
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// The SPA is served from a different origin in development only; in production the
// built frontend is expected to sit behind the same host as the API.
const string DevCors = "dev-spa";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.UseCors(DevCors);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapCvEndpoints();
app.MapTailorEndpoints();

app.Run();
