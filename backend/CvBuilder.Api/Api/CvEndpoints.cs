using CvBuilder.Api.Data;
using CvBuilder.Api.Domain;
using CvBuilder.Api.Pdf;
using Microsoft.EntityFrameworkCore;

namespace CvBuilder.Api.Api;

public static class CvEndpoints
{
    public static void MapCvEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        MapCvs(api);
        MapSections(api);
        MapItems(api);
        MapBullets(api);
    }

    // ---- CVs --------------------------------------------------------------

    private static void MapCvs(IEndpointRouteBuilder api)
    {
        api.MapGet("/cvs", async (CvDbContext db) =>
            Results.Ok(await db.Cvs
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new CvSummaryDto(c.Id, c.Name, c.FullName, c.UpdatedAt))
                .ToListAsync()));

        api.MapGet("/cvs/{id:guid}", async (Guid id, CvDbContext db) =>
        {
            var cv = await LoadFull(db, id);
            return cv is null ? Results.NotFound() : Results.Ok(Mapper.ToDto(cv));
        });

        api.MapPost("/cvs", async (CvHeaderRequest? req, CvDbContext db) =>
        {
            // A brand new CV starts from a template so the editor is never a blank page.
            var cv = Templates.NewStarterCv();
            if (req is not null) ApplyHeader(cv, req);

            db.Cvs.Add(cv);
            await db.SaveChangesAsync();
            return Results.Created($"/api/cvs/{cv.Id}", Mapper.ToDto(cv));
        });

        api.MapPut("/cvs/{id:guid}", async (Guid id, CvHeaderRequest req, CvDbContext db) =>
        {
            var cv = await db.Cvs.FindAsync(id);
            if (cv is null) return Results.NotFound();

            ApplyHeader(cv, req);
            await Touch(db, cv);
            return Results.Ok(Mapper.ToSummary(cv));
        });

        api.MapDelete("/cvs/{id:guid}", async (Guid id, CvDbContext db) =>
        {
            var cv = await db.Cvs.FindAsync(id);
            if (cv is null) return Results.NotFound();

            db.Cvs.Remove(cv);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapGet("/cvs/{id:guid}/pdf", async (Guid id, CvDbContext db) =>
        {
            var cv = await LoadFull(db, id);
            if (cv is null) return Results.NotFound();

            var bytes = CvPdfGenerator.Render(cv);
            var fileName = Slug(string.IsNullOrWhiteSpace(cv.FullName) ? cv.Name : cv.FullName) + "-cv.pdf";
            return Results.File(bytes, "application/pdf", fileName);
        });

        api.MapPost("/cvs/{id:guid}/sections", async (Guid id, SectionRequest req, CvDbContext db) =>
        {
            var cv = await db.Cvs.Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == id);
            if (cv is null) return Results.NotFound();

            var section = new Section
            {
                CvId = cv.Id,
                Title = Text(req.Title, 120, "New section"),
                Kind = req.Kind,
                Included = req.Included,
                SortOrder = NextOrder(cv.Sections.Select(s => s.SortOrder))
            };
            db.Sections.Add(section);
            await Touch(db, cv);
            return Results.Created($"/api/sections/{section.Id}", Mapper.ToDto(section));
        });

        api.MapPut("/cvs/{id:guid}/sections/order", async (Guid id, ReorderRequest req, CvDbContext db) =>
        {
            var cv = await db.Cvs.Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == id);
            if (cv is null) return Results.NotFound();

            Reorder(cv.Sections, req.Ids, s => s.Id, (s, o) => s.SortOrder = o);
            await Touch(db, cv);
            return Results.NoContent();
        });
    }

    // ---- Sections ---------------------------------------------------------

    private static void MapSections(IEndpointRouteBuilder api)
    {
        api.MapPut("/sections/{id:guid}", async (Guid id, SectionRequest req, CvDbContext db) =>
        {
            var section = await db.Sections.Include(s => s.Cv).FirstOrDefaultAsync(s => s.Id == id);
            if (section is null) return Results.NotFound();

            section.Title = Text(req.Title, 120, "Untitled section");
            section.Kind = req.Kind;
            section.Included = req.Included;
            await Touch(db, section.Cv);
            return Results.NoContent();
        });

        api.MapDelete("/sections/{id:guid}", async (Guid id, CvDbContext db) =>
        {
            var section = await db.Sections.Include(s => s.Cv).FirstOrDefaultAsync(s => s.Id == id);
            if (section is null) return Results.NotFound();

            db.Sections.Remove(section);
            await Touch(db, section.Cv);
            return Results.NoContent();
        });

        api.MapPost("/sections/{id:guid}/items", async (Guid id, ItemRequest req, CvDbContext db) =>
        {
            var section = await db.Sections
                .Include(s => s.Items)
                .Include(s => s.Cv)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (section is null) return Results.NotFound();

            var item = new CvItem
            {
                SectionId = section.Id,
                Title = Text(req.Title, 200),
                Organization = Text(req.Organization, 200),
                Location = Text(req.Location, 120),
                StartDate = Text(req.StartDate, 40),
                EndDate = Text(req.EndDate, 40),
                Included = req.Included,
                SortOrder = NextOrder(section.Items.Select(i => i.SortOrder))
            };
            db.Items.Add(item);
            await Touch(db, section.Cv);
            return Results.Created($"/api/items/{item.Id}", Mapper.ToDto(item));
        });

        api.MapPut("/sections/{id:guid}/items/order", async (Guid id, ReorderRequest req, CvDbContext db) =>
        {
            var section = await db.Sections
                .Include(s => s.Items)
                .Include(s => s.Cv)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (section is null) return Results.NotFound();

            Reorder(section.Items, req.Ids, i => i.Id, (i, o) => i.SortOrder = o);
            await Touch(db, section.Cv);
            return Results.NoContent();
        });
    }

    // ---- Items ------------------------------------------------------------

    private static void MapItems(IEndpointRouteBuilder api)
    {
        api.MapPut("/items/{id:guid}", async (Guid id, ItemRequest req, CvDbContext db) =>
        {
            var item = await db.Items
                .Include(i => i.Section).ThenInclude(s => s!.Cv)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (item is null) return Results.NotFound();

            item.Title = Text(req.Title, 200);
            item.Organization = Text(req.Organization, 200);
            item.Location = Text(req.Location, 120);
            item.StartDate = Text(req.StartDate, 40);
            item.EndDate = Text(req.EndDate, 40);
            item.Included = req.Included;
            await Touch(db, item.Section?.Cv);
            return Results.NoContent();
        });

        api.MapDelete("/items/{id:guid}", async (Guid id, CvDbContext db) =>
        {
            var item = await db.Items
                .Include(i => i.Section).ThenInclude(s => s!.Cv)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (item is null) return Results.NotFound();

            db.Items.Remove(item);
            await Touch(db, item.Section?.Cv);
            return Results.NoContent();
        });

        api.MapPost("/items/{id:guid}/bullets", async (Guid id, BulletRequest req, CvDbContext db) =>
        {
            var item = await db.Items
                .Include(i => i.Bullets)
                .Include(i => i.Section).ThenInclude(s => s!.Cv)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (item is null) return Results.NotFound();

            var bullet = new Bullet
            {
                ItemId = item.Id,
                Text = Text(req.Text, 1000),
                Included = req.Included,
                SortOrder = NextOrder(item.Bullets.Select(b => b.SortOrder))
            };
            db.Bullets.Add(bullet);
            await Touch(db, item.Section?.Cv);
            return Results.Created($"/api/bullets/{bullet.Id}", Mapper.ToDto(bullet));
        });

        api.MapPut("/items/{id:guid}/bullets/order", async (Guid id, ReorderRequest req, CvDbContext db) =>
        {
            var item = await db.Items
                .Include(i => i.Bullets)
                .Include(i => i.Section).ThenInclude(s => s!.Cv)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (item is null) return Results.NotFound();

            Reorder(item.Bullets, req.Ids, b => b.Id, (b, o) => b.SortOrder = o);
            await Touch(db, item.Section?.Cv);
            return Results.NoContent();
        });
    }

    // ---- Bullets ----------------------------------------------------------

    private static void MapBullets(IEndpointRouteBuilder api)
    {
        api.MapPut("/bullets/{id:guid}", async (Guid id, BulletRequest req, CvDbContext db) =>
        {
            var bullet = await db.Bullets
                .Include(b => b.Item).ThenInclude(i => i!.Section).ThenInclude(s => s!.Cv)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (bullet is null) return Results.NotFound();

            bullet.Text = Text(req.Text, 1000);
            bullet.Included = req.Included;
            await Touch(db, bullet.Item?.Section?.Cv);
            return Results.NoContent();
        });

        api.MapDelete("/bullets/{id:guid}", async (Guid id, CvDbContext db) =>
        {
            var bullet = await db.Bullets
                .Include(b => b.Item).ThenInclude(i => i!.Section).ThenInclude(s => s!.Cv)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (bullet is null) return Results.NotFound();

            db.Bullets.Remove(bullet);
            await Touch(db, bullet.Item?.Section?.Cv);
            return Results.NoContent();
        });
    }

    // ---- Helpers ----------------------------------------------------------

    private static Task<Cv?> LoadFull(CvDbContext db, Guid id) =>
        db.Cvs
            .Include(c => c.Sections).ThenInclude(s => s.Items).ThenInclude(i => i.Bullets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

    private static void ApplyHeader(Cv cv, CvHeaderRequest req)
    {
        cv.Name = Text(req.Name, 120, "My CV");
        cv.FullName = Text(req.FullName, 200);
        cv.Headline = Text(req.Headline, 200);
        cv.Email = Text(req.Email, 200);
        cv.Phone = Text(req.Phone, 60);
        cv.Location = Text(req.Location, 120);
        cv.Website = Text(req.Website, 200);
        cv.Summary = Text(req.Summary, 4000);
        cv.Style = req.Style;
    }

    private static async Task Touch(CvDbContext db, Cv? cv)
    {
        if (cv is not null) cv.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Trim, clamp to the column width, and fall back when empty.</summary>
    private static string Text(string? value, int maxLength, string fallback = "")
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0) return fallback;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static int NextOrder(IEnumerable<int> existing)
    {
        var orders = existing.ToList();
        return orders.Count == 0 ? 0 : orders.Max() + 1;
    }

    /// <summary>
    /// Applies the client's ordering. Rows the client did not mention (e.g. added by
    /// another tab) keep their relative order and are appended after the listed ones.
    /// </summary>
    private static void Reorder<T>(
        List<T> rows, List<Guid> orderedIds, Func<T, Guid> idOf, Action<T, int> setOrder)
    {
        var position = orderedIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        var ordered = rows
            .OrderBy(r => position.TryGetValue(idOf(r), out var p) ? p : int.MaxValue)
            .ToList();

        for (var i = 0; i < ordered.Count; i++) setOrder(ordered[i], i);
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
