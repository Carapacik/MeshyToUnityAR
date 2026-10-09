using System;
namespace StreetArt
{
    [Serializable] public sealed class LibraryItem { public string id, name, description; }
    [Serializable] public sealed class LibraryResponse { public LibraryItem[] items; }
    [Serializable] public sealed class Placement
    {
        public string id, modelId;
        public double latitude, longitude;
        public float scale, rotation, accuracyMeters;
    }
    [Serializable] public sealed class PlacementsResponse { public Placement[] items; }
    [Serializable] public sealed class GenerationCreated { public string id; }
    [Serializable] public sealed class GenerationTask { public string id, status, modelUrl, error; public int progress; }
    [Serializable] public sealed class GenerateInput { public string prompt; }
    [Serializable] public sealed class ServerConfig { public bool generationConfigured; }
    [Serializable] public sealed class ApiError { public string code, message; }
}
