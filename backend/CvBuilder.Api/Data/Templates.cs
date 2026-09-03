using CvBuilder.Api.Domain;

namespace CvBuilder.Api.Data;

/// <summary>The scaffold a new CV starts from, so the editor is never an empty page.</summary>
public static class Templates
{
    public static Cv NewStarterCv()
    {
        var cv = new Cv
        {
            Name = "My CV",
            FullName = "Your Name",
            Headline = "Your job title",
            Email = "you@example.com",
            Summary = "One or two sentences about what you do and what you are looking for.",
            Sections =
            [
                new Section
                {
                    Title = "Experience",
                    Kind = SectionKind.Timeline,
                    Items =
                    [
                        new CvItem
                        {
                            Title = "Job title",
                            Organization = "Company",
                            Location = "City",
                            StartDate = "2022",
                            EndDate = "Present",
                            Bullets =
                            [
                                new Bullet { Text = "What you built or owned, and the result." },
                                new Bullet { Text = "A second achievement, with a number if you have one." }
                            ]
                        }
                    ]
                },
                new Section
                {
                    Title = "Education",
                    Kind = SectionKind.Timeline,
                    Items =
                    [
                        new CvItem
                        {
                            Title = "Degree",
                            Organization = "University",
                            StartDate = "2018",
                            EndDate = "2021"
                        }
                    ]
                },
                new Section
                {
                    Title = "Skills",
                    Kind = SectionKind.Grouped,
                    Items =
                    [
                        new CvItem
                        {
                            Title = "Languages",
                            Bullets =
                            [
                                new Bullet { Text = "C#" },
                                new Bullet { Text = "TypeScript" },
                                new Bullet { Text = "SQL" }
                            ]
                        }
                    ]
                }
            ]
        };

        CvRefs.EnsureAll(cv);
        return cv;
    }
}
