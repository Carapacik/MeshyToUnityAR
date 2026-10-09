using Microsoft.Extensions.Options;
using StreetArt.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<MeshyOptions>(builder.Configuration.GetSection("Meshy"));
string directory = Path.GetFullPath(builder.Configuration["Storage:Directory"] ?? "Data", builder.Environment.ContentRootPath);
builder.Services.AddSingleton(new PlacementStore(directory));
builder.Services.AddSingleton(sp => new ModelFiles(directory, sp.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddHttpClient("Models", c => c.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddHttpClient<MeshyClient>((sp, c) =>
{
    c.BaseAddress = new Uri("https://api.meshy.ai/openapi/");
    c.Timeout = TimeSpan.FromSeconds(Math.Clamp(sp.GetRequiredService<IOptions<MeshyOptions>>().Value.TimeoutSeconds,5,180));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (ApiFailure e) { await Error(context,e.Status,e.Code,e.Message); }
    catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { await Error(context,504,"provider_timeout","The generation provider did not respond in time."); }
    catch (HttpRequestException) { await Error(context,502,"provider_unavailable","Could not reach the generation provider."); }
    catch (System.Text.Json.JsonException) { await Error(context,502,"invalid_provider_response","The provider returned an invalid response."); }
});
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/config", (IOptions<MeshyOptions> options) => new { generationConfigured = !string.IsNullOrWhiteSpace(options.Value.ApiKey) });
app.MapGet("/api/library", () => new { items = Library.Models });
app.MapPost("/api/generations", async (GenerateInput input, MeshyClient client, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Prompt) || input.Prompt.Trim().Length > 800)
        throw new ApiFailure(400,"invalid_prompt","Enter a description between 1 and 800 characters.");
    var task = await client.Create(input.Prompt.Trim(),ct);
    return Results.Accepted("/api/generations/" + task.Id, task);
});
app.MapGet("/api/generations/{id}", (string id, MeshyClient client, CancellationToken ct) => client.Get(id, ct));
app.MapPost("/api/generations/{id}/refine", async (string id, MeshyClient client, CancellationToken ct) =>
{
    var task = await client.Refine(id,ct);
    return Results.Accepted("/api/generations/" + task.Id,task);
});
app.MapGet("/api/models/{id}", (string id, ModelFiles files) =>
    files.Contains(id) ? Results.File(files.PathFor(id),"model/gltf-binary",enableRangeProcessing:true) : Results.NotFound());
app.MapGet("/api/placements", (double latitude, double longitude, double? radiusMeters, PlacementStore store) =>
{
    ValidateCoordinates(latitude,longitude);
    double radius = radiusMeters ?? 100;
    if (!double.IsFinite(radius) || radius < 1 || radius > 1000) throw new ApiFailure(400,"invalid_radius","Radius must be between 1 and 1000 meters.");
    return new { items = store.Nearby(latitude,longitude,radius) };
});
app.MapPost("/api/placements", (PlacementInput input, PlacementStore store, ModelFiles models) =>
{
    ValidateCoordinates(input.Latitude,input.Longitude);
    if (string.IsNullOrWhiteSpace(input.ModelId) || (!Library.Contains(input.ModelId) && !models.Contains(input.ModelId)))
        throw new ApiFailure(400,"unknown_model","Select a library model or a completed generation.");
    if (!float.IsFinite(input.Scale) || input.Scale < .1f || input.Scale > 3 || !float.IsFinite(input.Rotation) || input.Rotation < 0 || input.Rotation >= 360)
        throw new ApiFailure(400,"invalid_transform","Scale must be 0.1 to 3 and rotation 0 to 359 degrees.");
    if(input.AccuracyMeters is { } accuracy && (!float.IsFinite(accuracy) || accuracy <= 0 || accuracy > 100))
        throw new ApiFailure(400,"invalid_accuracy","Location accuracy must be between 0 and 100 meters.");
    return Results.Json(store.Add(input), statusCode:201);
});
app.Run();
static void ValidateCoordinates(double latitude,double longitude)
{
    if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        throw new ApiFailure(400,"invalid_coordinates","Valid latitude and longitude are required.");
}
static Task Error(HttpContext context,int status,string code,string message)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(new { code, message },context.RequestAborted);
}
public partial class Program { }
