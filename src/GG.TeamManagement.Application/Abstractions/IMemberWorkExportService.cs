namespace GG.TeamManagement.Application.Abstractions;

public interface IMemberWorkExportService
{
    Task<byte[]> ExportAsync(CancellationToken cancellationToken = default);
}
