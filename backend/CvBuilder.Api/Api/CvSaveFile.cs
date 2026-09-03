using System.Text.Json;
using System.Text.Json.Serialization;
using CvBuilder.Api.Domain;

namespace CvBuilder.Api.Api;

/// <summary>
/// The on-disk save file. Deliberately self-contained and id-free: order comes from
/// array order, and importing always mints fresh rows, so a file can be imported
/// repeatedly, on any machine, without colliding with what is already stored.
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
    string Title,
    SectionKind Kind,
    bool Included,
    List<SavedItem> Items);

public record SavedItem(
    string Title,
    string Organization,
    string Location,
    string StartDate,
    string EndDate,
    bool Included,
    List<SavedBullet> Bullets);

public record SavedBullet(string Text, bool Included);

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
            cv.Sections.OrderBy(s => s.SortOrder).Select(section => new SavedSection(
                section.Title, section.Kind, section.Included,
                section.Items.OrderBy(i => i.SortOrder).Select(item => new SavedItem(
                    item.Title, item.Organization, item.Location, item.StartDate, item.EndDate,
                    item.Included,
                    item.Bullets.OrderBy(b => b.SortOrder)
                        .Select(bullet => new SavedBullet(bullet.Text, bullet.Included))
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
            Sections = (saved.Sections ?? []).Select((section, sectionIndex) => new Section
            {
                Title = FieldText.Clamp(section.Title, 120, "Untitled section"),
                Kind = section.Kind,
                Included = section.Included,
                SortOrder = sectionIndex,
                Items = (section.Items ?? []).Select((item, itemIndex) => new CvItem
                {
                    Title = FieldText.Clamp(item.Title, 200),
                    Organization = FieldText.Clamp(item.Organization, 200),
                    Location = FieldText.Clamp(item.Location, 120),
                    StartDate = FieldText.Clamp(item.StartDate, 40),
                    EndDate = FieldText.Clamp(item.EndDate, 40),
                    Included = item.Included,
                    SortOrder = itemIndex,
                    Bullets = (item.Bullets ?? []).Select((bullet, bulletIndex) => new Bullet
                    {
                        Text = FieldText.Clamp(bullet.Text, 1000),
                        Included = bullet.Included,
                        SortOrder = bulletIndex
                    }).ToList()
                }).ToList()
            }).ToList()
        };

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
