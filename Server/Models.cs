namespace StreetArt.Server;

public sealed record LibraryModel(string Id, string Name, string Description);
public static class Library
{
    public static readonly LibraryModel[] Models =
    [
        new("rocket", "Little rocket", "A tiny explorer ready for takeoff."),
        new("tree", "Pocket tree", "A little green in an unexpected place."),
        new("sculpture", "Balance", "A colorful stack of playful shapes.")
    ];
    public static bool Contains(string id) => Models.Any(x => x.Id == id);
}
public sealed record PlacementInput(string ModelId, double Latitude, double Longitude, float Scale, float Rotation, float? AccuracyMeters = null);
public sealed record Placement(string Id, string ModelId, double Latitude, double Longitude, float Scale, float Rotation, DateTimeOffset CreatedAt, float? AccuracyMeters = null);
public sealed record GenerateInput(string Prompt);
public sealed record GenerationCreated(string Id);
public sealed record GenerationTask(string Id, string Status, int Progress, string? ModelUrl, string? Error);
public sealed class MeshyOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "meshy-t2";
    public int TimeoutSeconds { get; set; } = 45;
}
public sealed class ApiFailure(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
