using CvBuilder.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CvBuilder.Api.Data;

public static class CvQueries
{
    /// <summary>The whole CV graph — what the PDF, the save file and the tailoring all need.</summary>
    public static Task<Cv?> LoadFull(this CvDbContext db, Guid id) =>
        db.Cvs
            .Include(c => c.Sections).ThenInclude(s => s.Items).ThenInclude(i => i.Bullets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
}
