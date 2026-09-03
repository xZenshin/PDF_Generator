using CvBuilder.Api.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CvBuilder.Api.Pdf;

/// <summary>
/// Renders the *included* slice of a CV to A4. Everything the user has unchecked
/// is filtered out here, so the PDF is the single source of truth for "what got exported".
/// </summary>
public static class CvPdfGenerator
{
    private const string Accent = "#1f2937";  // near-black headings
    private const string Muted = "#6b7280";   // dates, locations
    private const string Rule = "#d1d5db";    // hairlines

    public static byte[] Render(Cv cv)
    {
        var sections = VisibleSections(cv);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).LineHeight(1.35f).FontColor(Accent));

                page.Content().Column(col =>
                {
                    col.Spacing(14);
                    ComposeHeader(col, cv);
                    if (!string.IsNullOrWhiteSpace(cv.Summary))
                        col.Item().Text(cv.Summary).FontColor(Muted);
                    foreach (var section in sections) ComposeSection(col, section);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    // ---- Composition ------------------------------------------------------

    private static void ComposeHeader(ColumnDescriptor col, Cv cv)
    {
        col.Item().Column(header =>
        {
            header.Spacing(2);

            if (!string.IsNullOrWhiteSpace(cv.FullName))
                header.Item().Text(cv.FullName).FontSize(22).SemiBold();

            if (!string.IsNullOrWhiteSpace(cv.Headline))
                header.Item().Text(cv.Headline).FontSize(11).FontColor(Muted);

            var contact = Join(" · ", cv.Email, cv.Phone, cv.Location, cv.Website);
            if (contact.Length > 0)
                header.Item().PaddingTop(4).Text(contact).FontSize(9).FontColor(Muted);

            header.Item().PaddingTop(8).LineHorizontal(0.8f).LineColor(Rule);
        });
    }

    private static void ComposeSection(ColumnDescriptor col, Section section)
    {
        col.Item().Column(block =>
        {
            block.Spacing(8);

            block.Item().Column(title =>
            {
                title.Item().Text(section.Title.ToUpperInvariant())
                    .FontSize(10).Bold().LetterSpacing(0.08f);
                title.Item().PaddingTop(2).LineHorizontal(0.6f).LineColor(Rule);
            });

            foreach (var item in VisibleItems(section))
            {
                switch (section.Kind)
                {
                    case SectionKind.Grouped:
                        block.Item().ShowEntire().Element(c => ComposeGroupedItem(c, item));
                        break;
                    case SectionKind.Bullets:
                        block.Item().Element(c => ComposeBulletList(c, item));
                        break;
                    default:
                        block.Item().Element(c => ComposeTimelineItem(c, item));
                        break;
                }
            }
        });
    }

    private static void ComposeTimelineItem(IContainer container, CvItem item)
    {
        container.Column(entry =>
        {
            entry.Spacing(3);

            entry.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(item.Title))
                        left.Item().Text(item.Title).SemiBold();
                    if (!string.IsNullOrWhiteSpace(item.Organization))
                        left.Item().Text(item.Organization).FontColor(Muted);
                });

                row.ConstantItem(150).AlignRight().Column(right =>
                {
                    var dates = Join(" – ", item.StartDate, item.EndDate);
                    if (dates.Length > 0)
                        right.Item().AlignRight().Text(dates).FontSize(9).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(item.Location))
                        right.Item().AlignRight().Text(item.Location).FontSize(9).FontColor(Muted);
                });
            });

            var bullets = VisibleBullets(item);
            if (bullets.Count > 0)
                entry.Item().PaddingTop(2).Element(c => ComposeBullets(c, bullets));
        });
    }

    private static void ComposeGroupedItem(IContainer container, CvItem item)
    {
        var values = Join(", ", VisibleBullets(item).Select(b => b.Text).ToArray());

        container.Row(row =>
        {
            row.ConstantItem(110).Text(item.Title).SemiBold();
            row.RelativeItem().Text(values);
        });
    }

    private static void ComposeBulletList(IContainer container, CvItem item)
    {
        var bullets = VisibleBullets(item);
        if (bullets.Count == 0) return;
        ComposeBullets(container, bullets);
    }

    private static void ComposeBullets(IContainer container, List<Bullet> bullets)
    {
        container.Column(list =>
        {
            list.Spacing(2);
            foreach (var bullet in bullets)
            {
                list.Item().Row(row =>
                {
                    row.ConstantItem(12).Text("•").FontColor(Muted);
                    row.RelativeItem().Text(bullet.Text);
                });
            }
        });
    }

    // ---- Inclusion filtering ---------------------------------------------

    /// <summary>Included sections that still have something to print once filtering is applied.</summary>
    private static List<Section> VisibleSections(Cv cv) => cv.Sections
        .Where(s => s.Included)
        .OrderBy(s => s.SortOrder)
        .Where(s => VisibleItems(s).Count > 0)
        .ToList();

    private static List<CvItem> VisibleItems(Section section) => section.Items
        .Where(i => i.Included)
        .OrderBy(i => i.SortOrder)
        .Where(i => HasContent(section, i))
        .ToList();

    private static List<Bullet> VisibleBullets(CvItem item) => item.Bullets
        .Where(b => b.Included && !string.IsNullOrWhiteSpace(b.Text))
        .OrderBy(b => b.SortOrder)
        .ToList();

    private static bool HasContent(Section section, CvItem item) => section.Kind switch
    {
        // These two render nothing but their bullets, so an item without any is dead weight.
        SectionKind.Bullets => VisibleBullets(item).Count > 0,
        SectionKind.Grouped => VisibleBullets(item).Count > 0,
        _ => !string.IsNullOrWhiteSpace(item.Title)
             || !string.IsNullOrWhiteSpace(item.Organization)
             || VisibleBullets(item).Count > 0
    };

    private static string Join(string separator, params string[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
