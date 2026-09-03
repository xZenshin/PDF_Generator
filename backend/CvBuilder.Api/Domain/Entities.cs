namespace CvBuilder.Api.Domain;

/// <summary>
/// A CV as the API works with it: built from a request body, used, and thrown away.
/// Nothing here is persisted — the browser holds the document and the save file is the
/// only durable copy. Order is list order, so there are no sort columns to keep straight.
/// </summary>
public class Cv
{
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
    /// <summary>Stable, human-readable handle (e.g. "exp"). See <see cref="CvRefs"/>.</summary>
    public string Ref { get; set; } = "";

    public string Title { get; set; } = "";
    public SectionKind Kind { get; set; } = SectionKind.Timeline;

    /// <summary>Excluded sections stay in the document but are left out of the PDF.</summary>
    public bool Included { get; set; } = true;

    /// <summary>
    /// Runs the section's bullets in two columns to save vertical space. Only
    /// <see cref="SectionKind.Bullets"/> honours it; carried but ignored elsewhere.
    /// </summary>
    public bool TwoColumns { get; set; }

    public List<CvItem> Items { get; set; } = [];
}

public class CvItem
{
    /// <summary>Stable, human-readable handle (e.g. "exp_i01").</summary>
    public string Ref { get; set; } = "";

    /// <summary>Job title, degree, project name, or a skill category.</summary>
    public string Title { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Location { get; set; } = "";

    /// <summary>Free text so "2019", "Mar 2019" and "Present" all work.</summary>
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    public bool Included { get; set; } = true;

    public List<Bullet> Bullets { get; set; } = [];
}

public class Bullet
{
    /// <summary>Stable, human-readable handle (e.g. "exp_003").</summary>
    public string Ref { get; set; } = "";

    public string Text { get; set; } = "";
    public bool Included { get; set; } = true;
}
