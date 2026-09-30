using ClosedXML.Excel;
using GG.TeamManagement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Infrastructure.Export;

public class ClosedXmlAttendanceExportService : IAttendanceExportService
{
    private readonly IApplicationDbContext _db;

    public ClosedXmlAttendanceExportService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var members = await _db.Members.AsNoTracking()
            .Include(m => m.Group)
            .OrderByDescending(m => m.Group.IsOfficeManagementTeam)
            .ThenBy(m => m.Group.Name)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);

        var sessions = await _db.MeetingAttendances.AsNoTracking()
            .Include(s => s.CustomQuestions)
            .Include(s => s.Records)
                .ThenInclude(r => r.Answers)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var grid = workbook.AddWorksheet("Attendance");
        var answersSheet = workbook.AddWorksheet("Custom answers");

        grid.Cell(1, 1).Value = "Member";
        grid.Cell(1, 2).Value = "Group";
        var dateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sessionLabels = sessions.Select(session =>
        {
            var key = session.Date.ToString("yyyy-MM-dd");
            dateCounts.TryGetValue(key, out var count);
            dateCounts[key] = count + 1;
            return count == 0 ? key : $"{key} ({count + 1})";
        }).ToList();

        for (var s = 0; s < sessions.Count; s++)
        {
            var col = 3 + (s * 2);
            grid.Cell(1, col).Value = $"{sessionLabels[s]} Present";
            grid.Cell(1, col + 1).Value = $"{sessionLabels[s]} Weekly class";
        }
        grid.Row(1).Style.Font.Bold = true;

        var recordsBySession = sessions.ToDictionary(
            s => s.Id,
            s => s.Records.ToDictionary(r => r.MemberId));

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var excelRow = i + 2;
            grid.Cell(excelRow, 1).Value = member.Name;
            grid.Cell(excelRow, 2).Value = member.Group.Name;

            for (var s = 0; s < sessions.Count; s++)
            {
                var col = 3 + (s * 2);
                if (recordsBySession[sessions[s].Id].TryGetValue(member.Id, out var record))
                {
                    grid.Cell(excelRow, col).Value = record.Present ? "Yes" : "No";
                    grid.Cell(excelRow, col + 1).Value = record.AttendedWeeklyClass ? "Yes" : "No";
                }
                else
                {
                    grid.Cell(excelRow, col).Value = "";
                    grid.Cell(excelRow, col + 1).Value = "";
                }
            }
        }

        answersSheet.Cell(1, 1).Value = "Member";
        answersSheet.Cell(1, 2).Value = "Group";
        answersSheet.Cell(1, 3).Value = "Date";
        answersSheet.Cell(1, 4).Value = "Question";
        answersSheet.Cell(1, 5).Value = "Answer";
        answersSheet.Row(1).Style.Font.Bold = true;

        var answerRow = 2;
        foreach (var session in sessions)
        {
            var questions = session.CustomQuestions.OrderBy(q => q.SortOrder).ToList();
            if (questions.Count == 0) continue;

            foreach (var member in members)
            {
                if (!recordsBySession[session.Id].TryGetValue(member.Id, out var record)) continue;
                var answers = record.Answers.ToDictionary(a => a.CustomQuestionId);
                foreach (var question in questions)
                {
                    answersSheet.Cell(answerRow, 1).Value = member.Name;
                    answersSheet.Cell(answerRow, 2).Value = member.Group.Name;
                    answersSheet.Cell(answerRow, 3).Value = session.Date.ToString("yyyy-MM-dd");
                    answersSheet.Cell(answerRow, 4).Value = question.QuestionText;
                    answersSheet.Cell(answerRow, 5).Value =
                        answers.TryGetValue(question.Id, out var answer) && answer.Value ? "Yes" : "No";
                    answerRow++;
                }
            }
        }

        grid.SheetView.FreezeRows(1);
        grid.SheetView.FreezeColumns(2);
        grid.Columns().AdjustToContents();
        answersSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
