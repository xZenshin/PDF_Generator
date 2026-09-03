namespace CvBuilder.Api.Ai;

/// <summary>
/// The instruction sent to DeepSeek as the system message.
///
/// ─────────────────────────────────────────────────────────────────────────────
///  THIS IS THE PROMPT TO REPLACE WITH YOUR OWN. It is the only place the
///  wording lives; nothing else in the codebase depends on how it is phrased,
///  only on the shape of the reply ({ "include": [...], "exclude": [...] }).
/// ─────────────────────────────────────────────────────────────────────────────
///
/// The placeholder below is a working default so the feature is testable before
/// you write yours.
/// </summary>
public static class TailoringPrompt
{
    public const string System = """
        You are helping tailor a CV to a specific job listing.

        You will receive a job listing followed by a CV as JSON. Every section, entry and
        bullet in the CV carries a stable "id" (for example "exp", "exp_i01", "exp_003").
        Each also carries an "included" flag showing whether it currently prints.

        Decide which parts of the CV should appear in an application for this job.
        Favour relevance to the listing over volume: a shorter, sharper CV is better than
        an exhaustive one. Keep anything the listing asks for, and drop material that
        neither supports the role nor establishes basic credibility.

        Reply with JSON only, in exactly this shape and with no commentary:

        {
          "include": ["exp_001", "exp_007", "exp_012"],
          "exclude": ["exp_002", "exp_003"]
        }

        Rules for the reply:
        - Use only ids that appear in the CV you were given.
        - "include" lists what should print; "exclude" lists what should not.
        - Anything you leave out of both lists keeps its current setting.
        - Do not invent, rewrite or reword any CV content. You are only choosing.
        """;
}
