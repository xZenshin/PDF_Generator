using System.Text;
using System.Text.Json;
using CvBuilder.Api.Ai;
using CvBuilder.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CvBuilder.Api.Api;

public record TailorRequest(string JobListing);

public record TailorResponse(
    string Model,
    TailoringRecommendation Recommendation,
    TailoringPlan Plan);

public record AiStatus(bool Configured, string Model);

public static class TailorEndpoints
{
    /// <summary>Guards against pasting an entire careers site into the prompt.</summary>
    private const int MaxJobListingLength = 20_000;

    public static void MapTailorEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/ai/status", (DeepSeekClient client) =>
            Results.Ok(new AiStatus(client.IsConfigured, client.Model)));

        // Asks the model which parts of this CV suit the listing, and works out what
        // that would change. Nothing is written — the user confirms first.
        api.MapPost("/cvs/{id:guid}/tailor", async (
            Guid id,
            TailorRequest req,
            CvDbContext db,
            DeepSeekClient client,
            CancellationToken ct) =>
        {
            var listing = (req.JobListing ?? "").Trim();
            if (listing.Length == 0)
                return Results.Problem("Paste the job listing first.", statusCode: StatusCodes.Status400BadRequest);
            if (listing.Length > MaxJobListingLength)
                return Results.Problem(
                    $"That job listing is {listing.Length:n0} characters; the limit is {MaxJobListingLength:n0}.",
                    statusCode: StatusCodes.Status400BadRequest);

            var cv = await db.LoadFull(id);
            if (cv is null) return Results.NotFound();

            try
            {
                var reply = await client.CompleteJsonAsync(TailoringPrompt.System, BuildMessage(listing, cv), ct);
                var recommendation = CvTailoring.Parse(reply);
                var plan = CvTailoring.Preview(cv, recommendation);
                return Results.Ok(new TailorResponse(client.Model, recommendation, plan));
            }
            catch (DeepSeekException ex)
            {
                // The user's problem to solve — a missing key, a rate limit, an odd reply.
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        // Applies a recommendation the user has seen. Takes the lists rather than the
        // plan, so the server recomputes against current data before writing.
        api.MapPost("/cvs/{id:guid}/tailor/apply", async (
            Guid id, TailoringRecommendation recommendation, CvDbContext db) =>
        {
            var cv = await db.LoadFull(id);
            if (cv is null) return Results.NotFound();

            var plan = CvTailoring.Apply(cv, recommendation);
            cv.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { plan, cv = Mapper.ToDto(cv) });
        });
    }

    /// <summary>
    /// The listing followed by the CV as a save file — the same JSON the user can
    /// download, so the ids the model replies with are ids they can see for themselves.
    /// </summary>
    private static string BuildMessage(string listing, Domain.Cv cv)
    {
        var saveFile = JsonSerializer.Serialize(CvSaveFiles.ToSaveFile(cv), SaveFileJson.Options);

        return new StringBuilder()
            .AppendLine("=== JOB LISTING ===")
            .AppendLine(listing)
            .AppendLine()
            .AppendLine("=== CV ===")
            .AppendLine(saveFile)
            .ToString();
    }
}
