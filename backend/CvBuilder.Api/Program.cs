using System.Text.Json.Serialization;
using CvBuilder.Api.Ai;
using CvBuilder.Api.Api;
using CvBuilder.Api.Data;
using CvBuilder.Api.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

// QuestPDF is free under the Community licence for individuals and small companies.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "No connection string. Set ConnectionStrings__Default (see docker-compose.yml).");

builder.Services.AddDbContext<CvDbContext>(o => o.UseNpgsql(connectionString));

// The DeepSeek key stays server-side. Set it with DeepSeek__ApiKey; the app runs
// fine without one, and the tailoring endpoint says so if it is missing.
var deepSeek = builder.Configuration.GetSection("DeepSeek").Get<DeepSeekOptions>() ?? new DeepSeekOptions();
builder.Services.AddSingleton(deepSeek);
builder.Services.AddHttpClient<DeepSeekClient>(c => c.Timeout = TimeSpan.FromSeconds(120));

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
        var starter = Templates.NewStarterCv();
        CvRefs.EnsureAll(starter);
        db.Cvs.Add(starter);
        await db.SaveChangesAsync();
    }

    await BackfillRefs(db);
}

if (app.Environment.IsDevelopment()) app.UseCors(DevCors);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapCvEndpoints();
app.MapTailorEndpoints();

app.Run();

/// <summary>
/// Gives refs to rows created before refs existed. Cheap to run and idempotent:
/// once every row has one, it finds nothing and does nothing.
/// </summary>
static async Task BackfillRefs(CvDbContext db)
{
    var needsRefs = await db.Cvs
        .Where(c => c.Sections.Any(s => s.Ref == ""
            || s.Items.Any(i => i.Ref == "" || i.Bullets.Any(b => b.Ref == ""))))
        .Select(c => c.Id)
        .ToListAsync();

    foreach (var id in needsRefs)
    {
        var cv = await db.LoadFull(id);
        if (cv is null) continue;
        CvRefs.EnsureAll(cv);
    }

    if (needsRefs.Count > 0) await db.SaveChangesAsync();
}
