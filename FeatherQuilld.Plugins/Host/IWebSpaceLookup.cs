namespace FeatherQuilld.Plugins.Host;

public interface IWebSpaceLookup
{
    WebSpaceSummary? Get(Guid uuid);
    IReadOnlyList<WebSpaceSummary> List();
}

public sealed record WebSpaceSummary(
    Guid Uuid,
    string Name,
    string Runtime,
    string Status);
