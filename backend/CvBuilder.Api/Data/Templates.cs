using CvBuilder.Api.Domain;

namespace CvBuilder.Api.Data;

/// <summary>The scaffold a new CV starts from, so the editor is never an empty page.</summary>
public static class Templates
{
    public static Cv NewStarterCv() => new()
    {
        Name = "My CV",
        FullName = "Your Name",
        Headline = "Your job title",
        Email = "you@example.com",
        Phone = "",
        Location = "",
        Website = "",
        Summary = "One or two sentences about what you do and what you are looking for.",
        Sections =
        [
            new Section
            {
                Title = "Experience",
                Kind = SectionKind.Timeline,
                SortOrder = 0,
                Items =
                [
                    new CvItem
                    {
                        Title = "Job title",
                        Organization = "Company",
                        Location = "City",
                        StartDate = "2022",
                        EndDate = "Present",
                        SortOrder = 0,
                        Bullets =
                        [
                            new Bullet { Text = "What you built or owned, and the result.", SortOrder = 0 },
                            new Bullet { Text = "A second achievement, with a number if you have one.", SortOrder = 1 }
                        ]
                    }
                ]
            },
            new Section
            {
                Title = "Education",
                Kind = SectionKind.Timeline,
                SortOrder = 1,
                Items =
                [
                    new CvItem
                    {
                        Title = "Degree",
                        Organization = "University",
                        StartDate = "2018",
                        EndDate = "2021",
                        SortOrder = 0
                    }
                ]
            },
            new Section
            {
                Title = "Skills",
                Kind = SectionKind.Grouped,
                SortOrder = 2,
                Items =
                [
                    new CvItem
                    {
                        Title = "Languages",
                        SortOrder = 0,
                        Bullets =
                        [
                            new Bullet { Text = "C#", SortOrder = 0 },
                            new Bullet { Text = "TypeScript", SortOrder = 1 },
                            new Bullet { Text = "SQL", SortOrder = 2 }
                        ]
                    }
                ]
            }
        ]
    };
}
