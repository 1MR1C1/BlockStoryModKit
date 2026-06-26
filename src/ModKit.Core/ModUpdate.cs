namespace ModKit.Core;

// what the GitHub updater reports for a mod that has a newer build available
public sealed class ModUpdate
{
    public string Name = "";
    public string? InstalledVersion;
    public string? AvailableVersion;
    public string SourcePath = "";
}
