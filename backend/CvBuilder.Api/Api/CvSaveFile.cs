using System.Text.Json;
using System.Text.Json.Serialization;
using CvBuilder.Api.Domain;

namespace CvBuilder.Api.Api;

/// <summary>
/// The on-disk save file — and, since the database was removed, the only durable copy
/// of a CV. Order is array order. Ids are the stable refs an LLM points at.
/// </summary>
public record CvSaveFile(
    string Format,
    int Version,
    DateTimeOffset ExportedAt,
    SavedCv Cv);

public record SavedCv(
    string Name,
    string FullName,
    string Headline,
    string Email,
    string Phone,
    string Location,
    string Website,
    string Summary,
    CvStyle Style,
    List<SavedSection> Sections);

public record SavedSection(
    string Id,
    string Title,
    SectionKind Kind,
    bool Included,
    bool TwoColumns,
    List<SavedItem> Items);

public record SavedItem(
    string Id,
    string Title,
    string Organization,
    string Location,
    string StartDate,
    string EndDate,
    bool Included,
    List<SavedBullet> Bullets);

public record SavedBullet(string Id, string Text, bool Included);

public static class CvSaveFiles
{
    /// <summary>Identifies our files, so an unrelated .json is rejected with a useful message.</summary>
    public const string FormatTag = "cvbuilder.cv";

    /// <summary>Bump when the shape changes; older versions stay importable.</summary>
    public const int CurrentVersion = 1;

    public static CvSaveFile ToSaveFile(Cv cv) => new(
        FormatTag,
        CurrentVersion,
        DateTimeOffset.UtcNow,
        new SavedCv(
            cv.Name, cv.FullName, cv.Headline, cv.Email, cv.Phone, cv.Location,
            cv.Website, cv.Summary, cv.Style,
            cv.Sections.Select(section => new SavedSection(
                section.Ref, section.Title, section.Kind, section.Included, section.TwoColumns,
                section.Items.Select(item => new SavedItem(
                    item.Ref, item.Title, item.Organization, item.Location,
                    item.StartDate, item.EndDate, item.Included,
                    item.Bullets
                        .Select(bullet => new SavedBullet(bullet.Ref, bullet.Text, bullet.Included))
                        .ToList()))
                    .ToList()))
                .ToList()));

    /// <summary>
    /// Rebuilds a CV from a save file. Returns null with a reason when the file is not
    /// one of ours, or was written by a newer version than this build understands.
    /// </summary>
    public static bool TryToEntity(CvSaveFile? file, out Cv cv, out string? problem)
    {
        cv = new Cv();

        if (file is null || file.Cv is null)
        {
            problem = "The file is empty or could not be read as a CV save file.";
            return false;
        }

        if (!string.Equals(file.Format, FormatTag, StringComparison.OrdinalIgnoreCase))
        {
            problem = $"Not a CV Builder save file (expected format \"{FormatTag}\").";
            return false;
        }

        if (file.Version > CurrentVersion)
        {
            problem = $"This file was written by a newer version of CV Builder "
                      + $"(file version {file.Version}, this build understands up to {CurrentVersion}).";
            return false;
        }

        var saved = file.Cv;
        cv = new Cv
        {
            Name = FieldText.Clamp(saved.Name, 120, "Imported CV"),
            FullName = FieldText.Clamp(saved.FullName, 200),
            Headline = FieldText.Clamp(saved.Headline, 200),
            Email = FieldText.Clamp(saved.Email, 200),
            Phone = FieldText.Clamp(saved.Phone, 60),
            Location = FieldText.Clamp(saved.Location, 120),
            Website = FieldText.Clamp(saved.Website, 200),
            Summary = FieldText.Clamp(saved.Summary, 4000),
            Style = saved.Style,
            Sections = (saved.Sections ?? []).Select(section => new Section
            {
                Ref = FieldText.Clamp(section.Id, 40),
                Title = FieldText.Clamp(section.Title, 120, "Untitled section"),
                Kind = section.Kind,
                Included = section.Included,
                TwoColumns = section.TwoColumns,
                Items = (section.Items ?? []).Select(item => new CvItem
                {
                    Ref = FieldText.Clamp(item.Id, 40),
                    Title = FieldText.Clamp(item.Title, 200),
                    Organization = FieldText.Clamp(item.Organization, 200),
                    Location = FieldText.Clamp(item.Location, 120),
                    StartDate = FieldText.Clamp(item.StartDate, 40),
                    EndDate = FieldText.Clamp(item.EndDate, 40),
                    Included = item.Included,
                    Bullets = (item.Bullets ?? []).Select(bullet => new Bullet
                    {
                        Ref = FieldText.Clamp(bullet.Id, 40),
                        Text = FieldText.Clamp(bullet.Text, 1000),
                        Included = bullet.Included
                    }).ToList()
                }).ToList()
            }).ToList()
        };

        // Ids in the file are kept so a model's reply about exp_003 still lands on the
        // right bullet; anything missing (a hand-written file) gets one assigned here.
        CvRefs.EnsureAll(cv);

        problem = null;
        return true;
    }
}

/// <summary>
/// Save files are serialised on their own terms rather than with the API's settings:
/// camelCase, named enums and indented, so the file reads well in an editor.
/// </summary>
public static class SaveFileJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
