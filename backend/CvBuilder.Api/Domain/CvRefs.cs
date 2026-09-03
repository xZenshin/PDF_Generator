namespace CvBuilder.Api.Domain;

/// <summary>
/// Assigns the stable, human-readable handles an LLM uses to point at parts of a CV:
/// <c>exp</c> for the Experience section, <c>exp_i01</c> for a job in it, <c>exp_003</c>
/// for one of its bullets. Bullets are numbered across the whole section, so the ids
/// read as a flat list of statements — which is how a model tends to reason about them.
///
/// A ref is assigned once and never changes: renaming a section leaves its refs alone,
/// because a ref is an identifier, not a description. That is what lets a save file on
/// disk, or a model's reply from last week, still mean something today.
/// </summary>
public static class CvRefs
{
    /// <summary>
    /// Short prefixes for the section names people actually use, so the model sees
    /// <c>exp_003</c> rather than <c>experi_003</c>.
    /// </summary>
    private static readonly Dictionary<string, string> KnownPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["experience"] = "exp",
        ["work experience"] = "exp",
        ["employment"] = "exp",
        ["employment history"] = "exp",
        ["education"] = "edu",
        ["skills"] = "skill",
        ["technical skills"] = "skill",
        ["projects"] = "proj",
        ["publications"] = "pub",
        ["certifications"] = "cert",
        ["certificates"] = "cert",
        ["languages"] = "lang",
        ["highlights"] = "high",
        ["summary"] = "sum",
        ["personal life"] = "personal",
        ["interests"] = "interest",
        ["volunteering"] = "vol"
    };

    /// <summary>
    /// Fills in every missing ref on a fully loaded CV, leaving existing ones untouched.
    /// Safe to call repeatedly — it is how imported files keep their ids and how rows
    /// created before refs existed get one.
    /// </summary>
    public static void EnsureAll(Cv cv)
    {
        var takenSectionRefs = cv.Sections
            .Where(s => !string.IsNullOrWhiteSpace(s.Ref))
            .Select(s => s.Ref)
            .ToList();

        foreach (var section in cv.Sections.OrderBy(s => s.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(section.Ref))
            {
                section.Ref = SectionRef(section.Title, takenSectionRefs);
                takenSectionRefs.Add(section.Ref);
            }

            var takenItemRefs = section.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.Ref))
                .Select(i => i.Ref)
                .ToList();

            var takenBulletRefs = section.Items
                .SelectMany(i => i.Bullets)
                .Where(b => !string.IsNullOrWhiteSpace(b.Ref))
                .Select(b => b.Ref)
                .ToList();

            foreach (var item in section.Items.OrderBy(i => i.SortOrder))
            {
                if (string.IsNullOrWhiteSpace(item.Ref))
                {
                    item.Ref = ItemRef(section.Ref, takenItemRefs);
                    takenItemRefs.Add(item.Ref);
                }

                foreach (var bullet in item.Bullets.OrderBy(b => b.SortOrder))
                {
                    if (!string.IsNullOrWhiteSpace(bullet.Ref)) continue;
                    bullet.Ref = BulletRef(section.Ref, takenBulletRefs);
                    takenBulletRefs.Add(bullet.Ref);
                }
            }
        }
    }

    public static string SectionRef(string title, IEnumerable<string> taken)
    {
        var prefix = Prefix(title);
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);

        if (!used.Contains(prefix)) return prefix;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = prefix + suffix;
            if (!used.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Entries within a section: <c>exp_i01</c>.</summary>
    public static string ItemRef(string sectionRef, IEnumerable<string> takenInSection) =>
        $"{sectionRef}_i{Next(takenInSection, $"{sectionRef}_i"):00}";

    /// <summary>Bullets, numbered across the whole section: <c>exp_003</c>.</summary>
    public static string BulletRef(string sectionRef, IEnumerable<string> takenInSection) =>
        $"{sectionRef}_{Next(takenInSection, $"{sectionRef}_"):000}";

    /// <summary>
    /// One past the highest number already used with this prefix. Suffixes that are not
    /// plain numbers are ignored, which is what keeps <c>exp_i01</c> out of the bullet
    /// sequence even though it shares the <c>exp_</c> prefix.
    /// </summary>
    private static int Next(IEnumerable<string> taken, string prefix)
    {
        var highest = 0;
        foreach (var candidate in taken)
        {
            if (candidate is null || !candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(candidate[prefix.Length..], out var number)) continue;
            if (number > highest) highest = number;
        }
        return highest + 1;
    }

    private static string Prefix(string title)
    {
        var cleaned = new string((title ?? "")
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray()).Trim();

        if (KnownPrefixes.TryGetValue(cleaned, out var wholeTitle)) return wholeTitle;

        var firstWord = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(firstWord)) return "sec";
        if (KnownPrefixes.TryGetValue(firstWord, out var known)) return known;

        return firstWord.Length <= 6 ? firstWord : firstWord[..6];
    }
}
