using System.Xml.Linq;

using Marten;

using Microsoft.AspNetCore.DataProtection.Repositories;

namespace buddy.Common.DataProtection;

// IXmlRepository is synchronous and Marten 8 is async-only, so this blocks on Marten's async calls.
// That is safe (ASP.NET Core has no synchronization context to deadlock on) and rare: the framework
// reads the key ring once and caches it, and writes only when it creates a key (every 90 days).
public sealed class MartenXmlRepository(IDataProtectionStore store) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var session = store.QuerySession();
        var documents = session.Query<DataProtectionKeyDocument>().ToListAsync().GetAwaiter().GetResult();
        return [.. documents.Select(d => XElement.Parse(d.Xml))];
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var session = store.LightweightSession();
        session.Store(new DataProtectionKeyDocument(friendlyName, element.ToString(SaveOptions.DisableFormatting)));
        session.SaveChangesAsync().GetAwaiter().GetResult();
    }
}
