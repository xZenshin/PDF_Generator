namespace CvBuilder.Api.Domain;

/// <summary>
/// The "master CV": one document holding every entry the user has ever written.
/// A PDF export is a filtered projection of it (see the Included flags below).
/// </summary>
public class Cv
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Name of the CV itself, e.g. "Master CV". Not printed.</summary>
    public string Name { get; set; } = "My CV";

    public string FullName { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Location { get; set; } = "";
    public string Website { get; set; } = "";
    public string Summary { get; set; } = "";

    /// <summary>Typography only — the layout is identical across styles.</summary>
    public CvStyle Style { get; set; } = CvStyle.Base;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Section> Sections { get; set; } = [];
}

/// <summary>
/// Visual treatment of a CV. Both styles print the same content in the same
/// arrangement — only type, rules, spacing and colour differ.
/// </summary>
public enum CvStyle
{
    /// <summary>Soft greys, semibold headings, hairline rules.</summary>
    Base = 0,

    /// <summary>Small-caps headings, heavy grey rules, black body text.</summary>
    Mono = 1
}

public enum SectionKind
{
    /// <summary>Title + org + dates + bullets. Experience, education, projects.</summary>
    Timeline = 0,

    /// <summary>Title acts as a category label, bullets are comma-joined onto one line.</summary>
    Grouped = 1,

    /// <summary>Bullets only, no item headers.</summary>
    Bullets = 2,

    /// <summary>Prose under the section title. Each bullet is a paragraph, unmarked.</summary>
    FreeForm = 3
}

public class Section
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CvId { get; set; }
    public Cv? Cv { get; set; }

    public string Title { get; set; } = "";
    public SectionKind Kind { get; set; } = SectionKind.Timeline;
    public int SortOrder { get; set; }

    /// <summary>Excluded sections stay in the database but are left out of the PDF.</summary>
    public bool Included { get; set; } = true;

    public List<CvItem> Items { get; set; } = [];
}

public class CvItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public Section? Section { get; set; }

    /// <summary>Job title, degree, project name, or a skill category.</summary>
    public string Title { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Location { get; set; } = "";

    /// <summary>Free text so "2019", "Mar 2019" and "Present" all work.</summary>
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    public int SortOrder { get; set; }
    public bool Included { get; set; } = true;

    public List<Bullet> Bullets { get; set; } = [];
}

public class Bullet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public CvItem? Item { get; set; }

    public string Text { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Included { get; set; } = true;
}
