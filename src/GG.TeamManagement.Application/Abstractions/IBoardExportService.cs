namespace GG.TeamManagement.Application.Abstractions;

public interface IBoardExportService
{
    Task<byte[]> ExportAsync(Guid groupId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);
}
