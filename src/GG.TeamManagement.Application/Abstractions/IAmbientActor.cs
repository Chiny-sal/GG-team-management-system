namespace GG.TeamManagement.Application.Abstractions;

public interface IAmbientActor
{
    string? Name { get; }
    Guid? MemberId { get; }
    Guid? GroupId { get; }
    bool ViaTelegram { get; }
    void Set(string? name, Guid? memberId, Guid? groupId, bool viaTelegram);
}

public sealed class AmbientActor : IAmbientActor
{
    public string? Name { get; private set; }
    public Guid? MemberId { get; private set; }
    public Guid? GroupId { get; private set; }
    public bool ViaTelegram { get; private set; }

    public void Set(string? name, Guid? memberId, Guid? groupId, bool viaTelegram)
    {
        Name = name;
        MemberId = memberId;
        GroupId = groupId;
        ViaTelegram = viaTelegram;
    }
}
