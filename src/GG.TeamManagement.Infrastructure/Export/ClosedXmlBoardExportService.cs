using ClosedXML.Excel;
using GG.TeamManagement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Infrastructure.Export;

public class ClosedXmlBoardExportService : IBoardExportService
{
    private readonly IApplicationDbContext _db;

    public ClosedXmlBoardExportService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportAsync(Guid groupId, DateOnly weekId, CancellationToken cancellationToken = default)
    {
        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new KeyNotFoundException("Group not found.");

        var members = await _db.Members.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

        var items = await _db.WorkItems.AsNoTracking()
            .Where(w => w.GroupId == groupId && w.WeekId == weekId && w.AssignedMemberId != null)
            .OrderBy(w => w.Title)
            .ToListAsync(cancellationToken);

        var maxWork = members
            .Select(m => items.Count(i => i.AssignedMemberId == m.Id))
            .DefaultIfEmpty(1)
            .Max();
        if (maxWork == 0) maxWork = 1;

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet($"{group.Name} {weekId:yyyy-MM-dd}");

        sheet.Cell(1, 1).Value = "Member";
        for (var i = 0; i < maxWork; i++)
        {
            var col = 2 + i * 3;
            sheet.Cell(1, col).Value = $"Work {i + 1} title";
            sheet.Cell(1, col + 1).Value = $"Work {i + 1} status";
            sheet.Cell(1, col + 2).Value = $"Work {i + 1} deadline";
        }

        sheet.Row(1).Style.Font.Bold = true;

        for (var row = 0; row < members.Count; row++)
        {
            var member = members[row];
            var assigned = items.Where(i => i.AssignedMemberId == member.Id).ToList();
            sheet.Cell(row + 2, 1).Value = member.Name;

            for (var i = 0; i < assigned.Count; i++)
            {
                var col = 2 + i * 3;
                sheet.Cell(row + 2, col).Value = assigned[i].Title;
                sheet.Cell(row + 2, col + 1).Value = assigned[i].Status.ToString();
                sheet.Cell(row + 2, col + 2).Value = assigned[i].Deadline.ToString("yyyy-MM-dd");
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
