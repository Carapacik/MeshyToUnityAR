namespace StreetArt.Server;

public sealed class ModelFiles(string directory, IHttpClientFactory factory)
{
    private readonly string root = Path.Combine(directory, "models");
    private readonly SemaphoreSlim gate = new(1, 1);
    public static bool IsId(string id) => Guid.TryParse(id, out _);
    public string PathFor(string id)
    {
        if (!IsId(id)) throw new ApiFailure(400, "invalid_task_id", "Invalid generation task ID.");
        return Path.Combine(root, Guid.Parse(id).ToString("N") + ".glb");
    }
    public bool Contains(string id) => IsId(id) && File.Exists(PathFor(id));
    public async Task Save(string id, Uri source, CancellationToken ct)
    {
        if (Contains(id)) return;
        if (source.Scheme != "https") throw new ApiFailure(502, "invalid_model_url", "Meshy returned an invalid model URL.");
        await gate.WaitAsync(ct);
        string path = PathFor(id);
        string temporary = path + ".partial";
        try
        {
            if (File.Exists(path)) return;
            Directory.CreateDirectory(root);
            using var response = await factory.CreateClient("Models").GetAsync(source, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw new ApiFailure(502, "model_download_failed", "Could not download the generated model.");
            const long limit = 64 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > limit) throw new ApiFailure(502, "model_too_large", "The model exceeds the 64 MB limit.");
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(temporary))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, ct)) != 0)
                {
                    total += count;
                    if (total > limit) throw new ApiFailure(502, "model_too_large", "The model exceeds the 64 MB limit.");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                }
            }
            var header = new byte[4];
            await using (var file = File.OpenRead(temporary)) { if (await file.ReadAsync(header,ct) != 4 || !header.SequenceEqual("glTF"u8.ToArray())) throw new ApiFailure(502,"invalid_model","The downloaded file is not a GLB model."); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}
