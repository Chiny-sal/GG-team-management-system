using ClosedXML.Excel;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Members;

namespace GG.TeamManagement.Infrastructure.Export;

public class ClosedXmlMemberWorkExportService : IMemberWorkExportService
{
    private readonly MemberService _members;

    public ClosedXmlMemberWorkExportService(MemberService members)
    {
        _members = members;
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _members.GetDirectoryAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Members");

        sheet.Cell(1, 1).Value = "Member";
        sheet.Cell(1, 2).Value = "Group";
        sheet.Cell(1, 3).Value = "Role";
        sheet.Cell(1, 4).Value = "Assigned";
        sheet.Cell(1, 5).Value = "Ongoing";
        sheet.Cell(1, 6).Value = "Done";
        sheet.Cell(1, 7).Value = "Not done";
        sheet.Cell(1, 8).Value = "Total assigned";
        sheet.Row(1).Style.Font.Bold = true;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            sheet.Cell(excelRow, 1).Value = row.Name;
            sheet.Cell(excelRow, 2).Value = row.GroupName;
            sheet.Cell(excelRow, 3).Value = row.Role.ToString();
            sheet.Cell(excelRow, 4).Value = row.AssignedCount;
            sheet.Cell(excelRow, 5).Value = row.OngoingCount;
            sheet.Cell(excelRow, 6).Value = row.DoneCount;
            sheet.Cell(excelRow, 7).Value = row.NotDoneCount;
            sheet.Cell(excelRow, 8).Value = row.TotalAssigned;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
