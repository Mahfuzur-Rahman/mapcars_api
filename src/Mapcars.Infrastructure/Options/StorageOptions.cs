namespace Mapcars.Infrastructure.Options;

public class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Directory documents are written to. Created if it doesn't exist.</summary>
    public string LocalPath { get; init; } = "App_Data/documents";
}
