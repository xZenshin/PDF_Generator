using System.Text.Json.Serialization;
using CvBuilder.Api.Api;
using CvBuilder.Api.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

// QuestPDF is free under the Community licence for individuals and small companies.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "No connection string. Set ConnectionStrings__Default (see docker-compose.yml).");

builder.Services.AddDbContext<CvDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    // SectionKind travels as "Timeline"/"Grouped"/"Bullets" rather than 0/1/2.
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CvDbContext>();
    db.Database.Migrate();

    // First run on a fresh database gets one CV so the editor has something to open.
    if (!await db.Cvs.AnyAsync())
    {
        db.Cvs.Add(Templates.NewStarterCv());
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment()) app.UseCors(DevCors);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapCvEndpoints();

app.Run();
