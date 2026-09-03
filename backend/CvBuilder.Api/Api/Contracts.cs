using CvBuilder.Api.Domain;

namespace CvBuilder.Api.Api;

// ---- Read models (what the client renders) --------------------------------

public record CvSummaryDto(Guid Id, string Name, string FullName, DateTimeOffset UpdatedAt);

public record CvDto(
    Guid Id,
    string Name,
    string FullName,
    string Headline,
    string Email,
    string Phone,
    string Location,
    string Website,
    string Summary,
    CvStyle Style,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SectionDto> Sections);

public record SectionDto(
    Guid Id,
    string Title,
    SectionKind Kind,
    int SortOrder,
    bool Included,
    IReadOnlyList<ItemDto> Items);

public record ItemDto(
    Guid Id,
    string Title,
    string Organization,
    string Location,
    string StartDate,
    string EndDate,
    int SortOrder,
    bool Included,
    IReadOnlyList<BulletDto> Bullets);

public record BulletDto(Guid Id, string Text, int SortOrder, bool Included);

// ---- Write models ---------------------------------------------------------

public record CvHeaderRequest(
    string Name,
    string FullName,
    string Headline,
    string Email,
    string Phone,
    string Location,
    string Website,
    string Summary,
    CvStyle Style);

public record SectionRequest(string Title, SectionKind Kind, bool Included);

public record ItemRequest(
    string Title,
    string Organization,
    string Location,
    string StartDate,
    string EndDate,
    bool Included);

public record BulletRequest(string Text, bool Included);

/// <summary>Ids in their new order. Anything omitted keeps its current position at the end.</summary>
public record ReorderRequest(List<Guid> Ids);

// ---- Mapping --------------------------------------------------------------

public static class Mapper
{
    public static CvSummaryDto ToSummary(Cv cv) => new(cv.Id, cv.Name, cv.FullName, cv.UpdatedAt);

    public static CvDto ToDto(Cv cv) => new(
        cv.Id, cv.Name, cv.FullName, cv.Headline, cv.Email, cv.Phone, cv.Location,
        cv.Website, cv.Summary, cv.Style, cv.UpdatedAt,
        cv.Sections.OrderBy(s => s.SortOrder).Select(ToDto).ToList());

    public static SectionDto ToDto(Section s) => new(
        s.Id, s.Title, s.Kind, s.SortOrder, s.Included,
        s.Items.OrderBy(i => i.SortOrder).Select(ToDto).ToList());

    public static ItemDto ToDto(CvItem i) => new(
        i.Id, i.Title, i.Organization, i.Location, i.StartDate, i.EndDate, i.SortOrder, i.Included,
        i.Bullets.OrderBy(b => b.SortOrder).Select(ToDto).ToList());

    public static BulletDto ToDto(Bullet b) => new(b.Id, b.Text, b.SortOrder, b.Included);
}
