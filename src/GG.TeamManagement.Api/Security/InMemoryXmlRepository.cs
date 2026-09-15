using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace GG.TeamManagement.Api.Security;

/// <summary>
/// Keeps Data Protection keys in process memory only. A single Render instance
/// does not need a shared key ring, and the default file repository writes under
/// a home directory that may be missing or ephemeral in the container.
/// </summary>
internal sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly List<XElement> _elements = [];
    private readonly object _lock = new();

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_lock)
            return _elements.Select(e => new XElement(e)).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = friendlyName;
        lock (_lock)
            _elements.Add(new XElement(element));
    }
}
