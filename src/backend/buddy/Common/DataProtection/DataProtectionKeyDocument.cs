namespace buddy.Common.DataProtection;

// One Data Protection key, as the XML the framework hands IXmlRepository.StoreElement. Id is the
// framework's friendly name ("key-<guid>"), unique per key.
public sealed record DataProtectionKeyDocument(string Id, string Xml);
