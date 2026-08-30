namespace GG.TeamManagement.Application.Abstractions;

public interface IBoardExportService
{
    Task<byte[]> ExportAsync(Guid groupId, DateOnly weekId, CancellationToken cancellationToken = default);
}
