using CvBuilder.Api.Domain;

namespace CvBuilder.Api.Pdf;

/// <summary>
/// The style sheet for a rendered CV. Every value here is typographic — type sizes,
/// weights, tracking, rule weights, colour and spacing. Nothing in this record can
/// move content around, which is what keeps the two styles the same document.
/// </summary>
public record CvTheme
{
    public required float BodySize { get; init; }
    public required float LineHeight { get; init; }

    public required string Ink { get; init; }
    public required string Muted { get; init; }
    public required string RuleColor { get; init; }

    public required float HeaderRuleWeight { get; init; }
    public required float SectionRuleWeight { get; init; }

    public required float NameSize { get; init; }
    public required bool NameUppercase { get; init; }
    public required float NameTracking { get; init; }
    public required float HeadlineSize { get; init; }
    public required float ContactSize { get; init; }
    public required string ContactSeparator { get; init; }
    public required string SummaryColor { get; init; }

    public required float SectionSize { get; init; }
    public required float SectionTracking { get; init; }
    public required bool SectionBold { get; init; }

    public required bool ItemTitleUppercase { get; init; }
    public required float ItemTitleSize { get; init; }
    public required float ItemTitleTracking { get; init; }
    public required string OrganizationColor { get; init; }

    public required string BulletGlyph { get; init; }
    public required float BulletGlyphSize { get; init; }

    /// <summary>Gap between the header, the summary and each section.</summary>
    public required float BlockSpacing { get; init; }

    /// <summary>Gap between a section heading and its entries, and between entries.</summary>
    public required float SectionSpacing { get; init; }

    public static CvTheme For(CvStyle style) => style == CvStyle.Mono ? Mono : Base;

    /// <summary>Soft greys, semibold headings, hairline rules.</summary>
    public static CvTheme Base { get; } = new()
    {
        BodySize = 10,
        LineHeight = 1.35f,

        Ink = "#1f2937",
        Muted = "#6b7280",
        RuleColor = "#d1d5db",

        HeaderRuleWeight = 0.8f,
        SectionRuleWeight = 0.6f,

        NameSize = 22,
        NameUppercase = false,
        NameTracking = 0f,
        HeadlineSize = 11,
        ContactSize = 9,
        ContactSeparator = " · ",
        SummaryColor = "#6b7280",

        SectionSize = 10,
        SectionTracking = 0.08f,
        SectionBold = true,

        ItemTitleUppercase = false,
        ItemTitleSize = 10,
        ItemTitleTracking = 0f,
        OrganizationColor = "#6b7280",

        BulletGlyph = "•",
        BulletGlyphSize = 10,

        BlockSpacing = 14,
        SectionSpacing = 8
    };

    /// <summary>
    /// Small-caps headings, heavy grey rules, black body text — the treatment used in
    /// the reference CV. Headings are set in tracked capitals rather than true small
    /// caps, which the bundled font does not carry.
    /// </summary>
    public static CvTheme Mono { get; } = new()
    {
        BodySize = 10,
        LineHeight = 1.3f,

        Ink = "#111827",
        Muted = "#4b5563",
        RuleColor = "#9ca3af",

        HeaderRuleWeight = 2.5f,
        SectionRuleWeight = 2.5f,

        NameSize = 20,
        NameUppercase = true,
        NameTracking = 0.06f,
        HeadlineSize = 10.5f,
        ContactSize = 8.5f,
        ContactSeparator = " | ",
        SummaryColor = "#111827",

        SectionSize = 12,
        SectionTracking = 0.1f,
        SectionBold = false,

        ItemTitleUppercase = true,
        ItemTitleSize = 9,
        ItemTitleTracking = 0.05f,
        OrganizationColor = "#111827",

        BulletGlyph = "●",
        BulletGlyphSize = 5,

        BlockSpacing = 16,
        SectionSpacing = 9
    };
}
