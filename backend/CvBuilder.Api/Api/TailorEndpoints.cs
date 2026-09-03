using System.Text;
using System.Text.Json;
using CvBuilder.Api.Ai;

namespace CvBuilder.Api.Api;

public record TailorRequest(string JobListing, CvSaveFile? Cv);

public record ApplyRequest(CvSaveFile? Cv, TailoringRecommendation? Recommendation);

public record TailorResponse(
    string Model,
    TailoringRecommendation Recommendation,
    TailoringPlan Plan);

public record ApplyResponse(TailoringPlan Plan, CvSaveFile Cv);

public record AiStatus(bool Configured, string Model, bool AuthRequired);

public static class TailorEndpoints
{
    /// <summary>Guards against pasting an entire careers site into the prompt.</summary>
    private const int MaxJobListingLength = 20_000;

    public static void MapTailorEndpoints(this IEndpointRouteBuilder app)
    {
        // Open: it says whether a key and a passphrase are wanted, never what they are.
        app.MapGet("/api/ai/status", (DeepSeekClient client, TailorAuthOptions auth) =>
            Results.Ok(new AiStatus(client.IsConfigured, client.Model, auth.IsRequired)));

        var api = app.MapGroup("/api/cv");

        // Asks the model which parts of the posted CV suit the listing, and works out
        // what that would change. Writes nothing anywhere — the user confirms first.
        api.MapPost("/tailor", async (
            TailorRequest req, DeepSeekClient client, CancellationToken ct) =>
        {
            var listing = (req.JobListing ?? "").Trim();
            if (listing.Length == 0)
                return Results.Problem("Paste the job listing first.", statusCode: StatusCodes.Status400BadRequest);
            if (listing.Length > MaxJobListingLength)
                return Results.Problem(
                    $"That job listing is {listing.Length:n0} characters; the limit is {MaxJobListingLength:n0}.",
                    statusCode: StatusCodes.Status400BadRequest);

            if (!CvSaveFiles.TryToEntity(req.Cv, out var cv, out var problem))
                return Results.Problem(problem, statusCode: StatusCodes.Status400BadRequest);

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
        }).RequirePassphrase();

        // Applies a recommendation the user has seen and returns the amended CV for the
        // editor to adopt. Takes the lists rather than the plan, so the decisions are
        // recomputed here rather than trusted from the client.
        api.MapPost("/tailor/apply", (ApplyRequest req) =>
        {
            if (!CvSaveFiles.TryToEntity(req.Cv, out var cv, out var problem))
                return Results.Problem(problem, statusCode: StatusCodes.Status400BadRequest);
            if (req.Recommendation is null)
                return Results.Problem("No recommendation to apply.", statusCode: StatusCodes.Status400BadRequest);

            var plan = CvTailoring.Apply(cv, req.Recommendation);
            return Results.Json(
                new ApplyResponse(plan, CvSaveFiles.ToSaveFile(cv)), SaveFileJson.Options);
        }).RequirePassphrase();
    }

    /// <summary>
    /// The listing followed by the CV as a save file — the same JSON the user can
    /// download, so the ids the model replies with are ids they could see for themselves.
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
