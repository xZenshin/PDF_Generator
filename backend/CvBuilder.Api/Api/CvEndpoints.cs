using System.Text.Json;
using CvBuilder.Api.Data;
using CvBuilder.Api.Domain;
using CvBuilder.Api.Pdf;

namespace CvBuilder.Api.Api;

/// <summary>
/// Stateless CV routes. The browser owns the document; every request carries the whole
/// CV in its body, gets used once, and is forgotten. There is nothing to store and
/// nothing to clean up.
/// </summary>
public static class CvEndpoints
{
    public static void MapCvEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/cv");

        // A starter CV for the "New" button, so the editor is never a blank page.
        api.MapGet("/template", () => Results.Json(
            CvSaveFiles.ToSaveFile(Templates.NewStarterCv()), SaveFileJson.Options));

        api.MapPost("/pdf", async (HttpRequest request, CancellationToken ct) =>
        {
            var (cv, problem) = await ReadCv(request, ct);
            if (cv is null) return problem!;

            var bytes = CvPdfGenerator.Render(cv);
            var fileName = Slug(string.IsNullOrWhiteSpace(cv.FullName) ? cv.Name : cv.FullName) + "-cv.pdf";
            return Results.File(bytes, "application/pdf", fileName);
        });

        // Round-trips the CV through validation and ref assignment, then hands back the
        // save file. This is the one place save files are written, so the format lives
        // in exactly one implementation.
        api.MapPost("/export", async (HttpRequest request, CancellationToken ct) =>
        {
            var (cv, problem) = await ReadCv(request, ct);
            if (cv is null) return problem!;

            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                CvSaveFiles.ToSaveFile(cv), SaveFileJson.Options);
            var fileName = Slug(string.IsNullOrWhiteSpace(cv.Name) ? cv.FullName : cv.Name) + ".cvjson";
            return Results.File(bytes, "application/json", fileName);
        });

        // Validates a file the user picked and returns it normalised, so the editor
        // never has to trust or repair what came off disk.
        api.MapPost("/import", async (HttpRequest request, CancellationToken ct) =>
        {
            var (cv, problem) = await ReadCv(request, ct);
            if (cv is null) return problem!;

            return Results.Json(CvSaveFiles.ToSaveFile(cv), SaveFileJson.Options);
        });
    }

    /// <summary>
    /// Reads a save file from the request body. Returns the CV, or the problem response
    /// to send instead — parsing by hand keeps the message readable when the body is
    /// not one of our files at all.
    /// </summary>
    internal static async Task<(Cv? Cv, IResult? Problem)> ReadCv(
        HttpRequest request, CancellationToken ct)
    {
        CvSaveFile? file;
        try
        {
            file = await JsonSerializer.DeserializeAsync<CvSaveFile>(
                request.Body, SaveFileJson.Options, ct);
        }
        catch (JsonException ex)
        {
            return (null, Results.Problem(
                $"That file could not be read as a CV save file: {ex.Message}",
                statusCode: StatusCodes.Status400BadRequest));
        }

        if (!CvSaveFiles.TryToEntity(file, out var cv, out var problem))
            return (null, Results.Problem(problem, statusCode: StatusCodes.Status400BadRequest));

        return (cv, null);
    }

    private static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrEmpty(slug) ? "my" : slug;
    }
}
