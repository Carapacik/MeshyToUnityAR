using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace StreetArt.Server;

public sealed class MeshyClient(HttpClient http, IOptions<MeshyOptions> options, ModelFiles models)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);
    private async Task<JsonDocument> Send(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (!IsConfigured) throw new ApiFailure(503, "generation_not_configured", "Generation is not configured. Use a model from the library.");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var (status, code, message) = (int)response.StatusCode switch
            {
                400 => (400, "generation_rejected", "Meshy rejected this generation request."),
                401 or 403 => (503, "provider_authentication_failed", "The server's Meshy key is not accepted."),
                402 => (402, "insufficient_credits", "The Meshy account has insufficient credits."),
                429 => (429, "provider_busy", "Meshy is busy. Please try again later."),
                404 => (404, "task_not_found", "Generation task not found."),
                _ => (502, "provider_unavailable", "Meshy is temporarily unavailable.")
            };
            throw new ApiFailure(status, code, message);
        }
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }
    public async Task<GenerationCreated> Create(string prompt, CancellationToken ct)
    {
        var body = new { mode = "preview", prompt, ai_model = options.Value.Model,
            model_type = options.Value.Model == "meshy-t2" ? "smart-topology" : "standard",
            topology = "triangle", target_polycount = 4000, target_formats = new[] { "glb" } };
        using var json = await Send(HttpMethod.Post, "v2/text-to-3d", body, ct);
        return Created(json);
    }
    public async Task<GenerationCreated> Refine(string id, CancellationToken ct)
    {
        var preview = await Get(id, ct);
        if (preview.Status != "SUCCEEDED") throw new ApiFailure(409, "preview_not_ready", "Wait for the preview to finish before adding textures.");
        using var json = await Send(HttpMethod.Post, "v2/text-to-3d", new { mode = "refine", preview_task_id = id, texture_resolution = "2k", target_formats = new[] { "glb" } }, ct);
        return Created(json);
    }
    private static GenerationCreated Created(JsonDocument json)
    {
        string? id = json.RootElement.GetProperty("result").GetString();
        if (id == null || !ModelFiles.IsId(id)) throw new ApiFailure(502, "invalid_provider_response", "Meshy returned an invalid task ID.");
        return new(id);
    }
    public async Task<GenerationTask> Get(string id, CancellationToken ct)
    {
        if (!ModelFiles.IsId(id)) throw new ApiFailure(400,"invalid_task_id","Invalid generation task ID.");
        if (models.Contains(id)) return new(id,"SUCCEEDED",100,"/api/models/" + id,null);
        using var json = await Send(HttpMethod.Get, "v2/text-to-3d/" + Uri.EscapeDataString(id), null, ct);
        var root = json.RootElement;
        string status = root.GetProperty("status").GetString() ?? "PENDING";
        int progress = root.TryGetProperty("progress", out var p) ? Math.Clamp(p.GetInt32(),0,100) : 0;
        string? modelUrl = null;
        if (status == "SUCCEEDED")
        {
            if (!root.TryGetProperty("model_urls",out var urls) || !urls.TryGetProperty("glb",out var glb) || !Uri.TryCreate(glb.GetString(),UriKind.Absolute,out var uri))
                throw new ApiFailure(502,"model_missing","Meshy completed the task without a GLB model.");
            await models.Save(id, uri, ct);
            modelUrl = "/api/models/" + id;
        }
        return new(id,status,progress,modelUrl,status is "FAILED" or "CANCELED" ? "Generation did not complete. Please try again." : null);
    }
}
