using CvBuilder.Api.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CvBuilder.Api.Pdf;

/// <summary>
/// Renders the *included* slice of a CV to A4. Everything the user has unchecked
/// is filtered out here, so the PDF is the single source of truth for "what got exported".
/// The arrangement is fixed; <see cref="CvTheme"/> supplies the typography.
/// </summary>
public static class CvPdfGenerator
{
    /// <summary>
    /// Bullets are drawn as a glyph in a fixed-width column, at body size so the two
    /// baselines agree. Preview.tsx mirrors both numbers.
    /// </summary>
    private const string BulletGlyph = "•";
    private const float BulletColumnWidth = 12;

    public static byte[] Render(Cv cv)
    {
        var theme = CvTheme.For(cv.Style);
        var sections = VisibleSections(cv);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(t => t
                    .FontSize(theme.BodySize)
                    .LineHeight(theme.LineHeight)
                    .FontColor(theme.Ink));

                page.Content().Column(col =>
                {
                    col.Spacing(theme.BlockSpacing);
                    ComposeHeader(col, cv, theme);
                    if (!string.IsNullOrWhiteSpace(cv.Summary))
                        col.Item().Text(cv.Summary).FontColor(theme.SummaryColor);
                    foreach (var section in sections) ComposeSection(col, section, theme);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(theme.Muted));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    // ---- Composition ------------------------------------------------------

    private static void ComposeHeader(ColumnDescriptor col, Cv cv, CvTheme theme)
    {
        col.Item().Column(header =>
        {
            header.Spacing(2);

            if (!string.IsNullOrWhiteSpace(cv.FullName))
                header.Item()
                    .Text(theme.NameUppercase ? cv.FullName.ToUpperInvariant() : cv.FullName)
                    .FontSize(theme.NameSize).SemiBold().LetterSpacing(theme.NameTracking);

            if (!string.IsNullOrWhiteSpace(cv.Headline))
                header.Item().Text(cv.Headline).FontSize(theme.HeadlineSize).FontColor(theme.Muted);

            var contact = Join(theme.ContactSeparator, cv.Email, cv.Phone, cv.Location, cv.Website);
            if (contact.Length > 0)
                header.Item().PaddingTop(4).Text(contact)
                    .FontSize(theme.ContactSize).FontColor(theme.Muted);

            header.Item().PaddingTop(8)
                .LineHorizontal(theme.HeaderRuleWeight).LineColor(theme.RuleColor);
        });
    }

    private static void ComposeSection(ColumnDescriptor col, Section section, CvTheme theme)
    {
        col.Item().Column(block =>
        {
            block.Spacing(theme.SectionSpacing);

            block.Item().Column(title =>
            {
                var heading = title.Item().Text(section.Title.ToUpperInvariant())
                    .FontSize(theme.SectionSize).LetterSpacing(theme.SectionTracking);
                if (theme.SectionBold) heading.Bold();

                title.Item().PaddingTop(2)
                    .LineHorizontal(theme.SectionRuleWeight).LineColor(theme.RuleColor);
            });

            // Two columns is a property of the section, not of any one entry: every
            // bullet in it becomes one list that spans both columns.
            if (section.Kind == SectionKind.Bullets && section.TwoColumns)
            {
                var all = VisibleItems(section).SelectMany(VisibleBullets).ToList();
                if (all.Count > 0) block.Item().Element(c => ComposeTwoColumnBullets(c, all, theme));
                return;
            }

            foreach (var item in VisibleItems(section))
            {
                switch (section.Kind)
                {
                    case SectionKind.Grouped:
                        block.Item().ShowEntire().Element(c => ComposeGroupedItem(c, item, theme));
                        break;
                    case SectionKind.Bullets:
                        block.Item().Element(c => ComposeBulletList(c, item, theme));
                        break;
                    case SectionKind.FreeForm:
                        block.Item().Element(c => ComposeProse(c, item));
                        break;
                    default:
                        block.Item().Element(c => ComposeTimelineItem(c, item, theme));
                        break;
                }
            }
        });
    }

    private static void ComposeTimelineItem(IContainer container, CvItem item, CvTheme theme)
    {
        container.Column(entry =>
        {
            entry.Spacing(3);

            entry.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(item.Title))
                        left.Item()
                            .Text(theme.ItemTitleUppercase ? item.Title.ToUpperInvariant() : item.Title)
                            .FontSize(theme.ItemTitleSize).SemiBold()
                            .LetterSpacing(theme.ItemTitleTracking);

                    if (!string.IsNullOrWhiteSpace(item.Organization))
                        left.Item().Text(item.Organization).FontColor(theme.OrganizationColor);
                });

                row.ConstantItem(150).AlignRight().Column(right =>
                {
                    var dates = Join(" – ", item.StartDate, item.EndDate);
                    if (dates.Length > 0)
                        right.Item().AlignRight().Text(dates)
                            .FontSize(theme.MetaSize).FontColor(theme.Muted);
                    if (!string.IsNullOrWhiteSpace(item.Location))
                        right.Item().AlignRight().Text(item.Location)
                            .FontSize(theme.MetaSize).FontColor(theme.Muted);
                });
            });

            var bullets = VisibleBullets(item);
            if (bullets.Count > 0)
                entry.Item().PaddingTop(2).Element(c => ComposeBullets(c, bullets, theme));
        });
    }

    private static void ComposeGroupedItem(IContainer container, CvItem item, CvTheme theme)
    {
        var values = Join(", ", VisibleBullets(item).Select(b => b.Text).ToArray());

        container.Row(row =>
        {
            row.ConstantItem(110)
                .Text(theme.ItemTitleUppercase ? item.Title.ToUpperInvariant() : item.Title)
                .FontSize(theme.ItemTitleSize).SemiBold().LetterSpacing(theme.ItemTitleTracking);
            row.RelativeItem().Text(values);
        });
    }

    private static void ComposeBulletList(IContainer container, CvItem item, CvTheme theme)
    {
        var bullets = VisibleBullets(item);
        if (bullets.Count == 0) return;
        ComposeBullets(container, bullets, theme);
    }

    /// <summary>Free-form prose: one paragraph per included bullet, no bullet glyph.</summary>
    private static void ComposeProse(IContainer container, CvItem item)
    {
        var paragraphs = VisibleBullets(item);
        if (paragraphs.Count == 0) return;

        container.Column(prose =>
        {
            prose.Spacing(6);
            foreach (var paragraph in paragraphs)
                prose.Item().Text(paragraph.Text).Justify();
        });
    }

    /// <summary>
    /// The same bullet list, filling the left column before the right so it reads
    /// top-to-bottom per column. An odd count leaves the extra bullet on the left.
    /// </summary>
    private static void ComposeTwoColumnBullets(IContainer container, List<Bullet> bullets, CvTheme theme)
    {
        var leftCount = (bullets.Count + 1) / 2;
        var left = bullets.Take(leftCount).ToList();
        var right = bullets.Skip(leftCount).ToList();

        container.Row(row =>
        {
            row.Spacing(18);
            row.RelativeItem().Element(c => ComposeBullets(c, left, theme));
            row.RelativeItem().Element(c =>
            {
                if (right.Count > 0) ComposeBullets(c, right, theme);
            });
        });
    }

    private static void ComposeBullets(IContainer container, List<Bullet> bullets, CvTheme theme)
    {
        container.Column(list =>
        {
            list.Spacing(2);
            foreach (var bullet in bullets)
            {
                list.Item().Row(row =>
                {
                    row.ConstantItem(BulletColumnWidth).Text(BulletGlyph).FontColor(theme.Muted);
                    row.RelativeItem().Text(bullet.Text);
                });
            }
        });
    }

    // ---- Inclusion filtering ---------------------------------------------

    /// <summary>Included sections that still have something to print once filtering is applied.</summary>
    private static List<Section> VisibleSections(Cv cv) => cv.Sections
        .Where(s => s.Included && VisibleItems(s).Count > 0)
        .ToList();

    private static List<CvItem> VisibleItems(Section section) => section.Items
        .Where(i => i.Included && HasContent(section, i))
        .ToList();

    private static List<Bullet> VisibleBullets(CvItem item) => item.Bullets
        .Where(b => b.Included && !string.IsNullOrWhiteSpace(b.Text))
        .ToList();

    private static bool HasContent(Section section, CvItem item) => section.Kind switch
    {
        // These render nothing but their bullets, so an item without any is dead weight.
        SectionKind.Bullets => VisibleBullets(item).Count > 0,
        SectionKind.Grouped => VisibleBullets(item).Count > 0,
        SectionKind.FreeForm => VisibleBullets(item).Count > 0,
        _ => !string.IsNullOrWhiteSpace(item.Title)
             || !string.IsNullOrWhiteSpace(item.Organization)
             || VisibleBullets(item).Count > 0
    };

    private static string Join(string separator, params string[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
