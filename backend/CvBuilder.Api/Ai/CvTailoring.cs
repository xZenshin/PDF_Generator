using System.Text.Json;
using CvBuilder.Api.Domain;

namespace CvBuilder.Api.Ai;

/// <summary>What the model replied: ids to print, and ids not to.</summary>
public record TailoringRecommendation(List<string>? Include, List<string>? Exclude);

/// <summary>One toggle that would flip, described well enough to read at a glance.</summary>
public record PlannedChange(
    string Ref,
    string Kind,
    string Label,
    bool Include,
    /// <summary>True when we added this ourselves so an included child can actually print.</summary>
    bool Cascaded);

public record TailoringPlan(
    List<PlannedChange> Changes,
    /// <summary>Ids the model named that already had the requested setting.</summary>
    int AlreadyCorrect,
    /// <summary>Ids that are not in this CV — usually the model inventing one.</summary>
    List<string> Unrecognised,
    /// <summary>Ids the model put in both lists. Treated as excluded.</summary>
    List<string> Contradictory);

/// <summary>
/// Turns a model's include/exclude reply into a set of toggle changes against a CV.
/// The same code computes the preview and performs the write, so what the user
/// confirms is exactly what happens.
/// </summary>
public static class CvTailoring
{
    /// <summary>Works out what would change, touching nothing.</summary>
    public static TailoringPlan Preview(Cv cv, TailoringRecommendation reply) =>
        Resolve(cv, reply, write: false);

    /// <summary>Applies the same decisions to the CV's Included flags.</summary>
    public static TailoringPlan Apply(Cv cv, TailoringRecommendation reply) =>
        Resolve(cv, reply, write: true);

    /// <summary>
    /// Parses the assistant's message. Models still fence their JSON now and then even
    /// when asked for a bare object, so the fences come off first.
    /// </summary>
    public static TailoringRecommendation Parse(string reply)
    {
        var json = Unfence(reply);
        try
        {
            var parsed = JsonSerializer.Deserialize<TailoringRecommendation>(
                json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (parsed is null || (parsed.Include is null && parsed.Exclude is null))
                throw new DeepSeekException(
                    $"The reply had no \"include\" or \"exclude\" list. It said: {Shorten(reply, 300)}");

            return parsed;
        }
        catch (JsonException)
        {
            throw new DeepSeekException($"The reply was not the expected JSON. It said: {Shorten(reply, 300)}");
        }
    }

    private static string Unfence(string reply)
    {
        var text = reply.Trim();
        if (!text.StartsWith("```")) return text;

        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0) return text;

        text = text[(firstBreak + 1)..];
        var closing = text.LastIndexOf("```", StringComparison.Ordinal);
        return (closing >= 0 ? text[..closing] : text).Trim();
    }

    // ---- Resolution -------------------------------------------------------

    private static TailoringPlan Resolve(Cv cv, TailoringRecommendation reply, bool write)
    {
        var nodes = Index(cv);
        var include = Clean(reply.Include);
        var exclude = Clean(reply.Exclude);

        // A reply naming the same id twice is contradictory; take the subtractive reading.
        var contradictory = include.Intersect(exclude, StringComparer.OrdinalIgnoreCase).ToList();
        include = include.Except(contradictory, StringComparer.OrdinalIgnoreCase).ToList();

        var unrecognised = include.Concat(exclude)
            .Where(r => !nodes.ContainsKey(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build the target state before touching anything, so contradictions between an
        // explicit exclude and an inherited include resolve once, predictably.
        var desired = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in exclude)
            if (nodes.ContainsKey(reference)) desired[reference] = false;
        foreach (var reference in include)
            if (nodes.ContainsKey(reference)) desired[reference] = true;

        // An included bullet inside an excluded entry would still not print, so pull its
        // ancestors in — unless the model excluded one on purpose, which wins.
        var cascaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in include)
        {
            if (!nodes.TryGetValue(reference, out var node)) continue;

            var parent = node.Parent;
            while (parent is not null && nodes.TryGetValue(parent, out var ancestor))
            {
                if (!desired.ContainsKey(ancestor.Ref))
                {
                    desired[ancestor.Ref] = true;
                    cascaded.Add(ancestor.Ref);
                }
                parent = ancestor.Parent;
            }
        }

        var changes = new List<PlannedChange>();
        var alreadyCorrect = 0;

        foreach (var (reference, shouldInclude) in desired)
        {
            var node = nodes[reference];
            if (node.Get() == shouldInclude)
            {
                // Only count what the model actually asked for; cascades are noise here.
                if (!cascaded.Contains(reference)) alreadyCorrect++;
                continue;
            }

            changes.Add(new PlannedChange(
                node.Ref, node.Kind, node.Label, shouldInclude, cascaded.Contains(reference)));

            if (write) node.Set(shouldInclude);
        }

        return new TailoringPlan(
            changes.OrderBy(c => nodes[c.Ref].Order).ToList(),
            alreadyCorrect,
            unrecognised,
            contradictory);
    }

    private static List<string> Clean(List<string>? refs) => (refs ?? [])
        .Where(r => !string.IsNullOrWhiteSpace(r))
        .Select(r => r.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private sealed record Node(
        string Ref, string Kind, string Label, Func<bool> Get, Action<bool> Set, string? Parent,
        /// <summary>Position in the CV, so changes can be listed the way the CV reads.</summary>
        int Order);

    private static Dictionary<string, Node> Index(Cv cv)
    {
        var nodes = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        var order = 0;

        foreach (var section in cv.Sections.OrderBy(s => s.SortOrder))
        {
            Add(new Node(section.Ref, "section", Label(section.Title, "untitled section"),
                () => section.Included, v => section.Included = v, null, order++));

            foreach (var item in section.Items.OrderBy(i => i.SortOrder))
            {
                Add(new Node(item.Ref, "entry", EntryLabel(item),
                    () => item.Included, v => item.Included = v, section.Ref, order++));

                foreach (var bullet in item.Bullets.OrderBy(b => b.SortOrder))
                    Add(new Node(bullet.Ref, "bullet", Label(bullet.Text, "empty line"),
                        () => bullet.Included, v => bullet.Included = v, item.Ref, order++));
            }
        }

        return nodes;

        void Add(Node node)
        {
            if (string.IsNullOrWhiteSpace(node.Ref)) return;
            nodes.TryAdd(node.Ref, node);
        }
    }

    // Labels are shown in full: the text is how the user identifies a change now that
    // ids are not on screen, so trimming it could make two bullets look like one.
    private static string EntryLabel(CvItem item)
    {
        var parts = new[] { item.Title, item.Organization }.Where(p => !string.IsNullOrWhiteSpace(p));
        var label = string.Join(" — ", parts);
        return string.IsNullOrWhiteSpace(label) ? "untitled entry" : label;
    }

    private static string Label(string text, string fallback) =>
        string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();

    private static string Shorten(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
