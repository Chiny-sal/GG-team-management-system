namespace GG.TeamManagement.Application.Abstractions;

public interface ICurrentWeekService
{
    DateOnly GetCurrentWeekId();
    DateOnly Refresh();
}
