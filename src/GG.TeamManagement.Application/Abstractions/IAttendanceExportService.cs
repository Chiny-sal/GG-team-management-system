namespace GG.TeamManagement.Application.Abstractions;

public interface IAttendanceExportService
{
    Task<byte[]> ExportAsync(CancellationToken cancellationToken = default);
}
