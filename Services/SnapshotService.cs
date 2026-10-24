using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace GlurSolutions.Web.Services;

public class SnapshotService(SiteDataBuilder builder, IConfiguration cfg, ILogger<SnapshotService> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Created on first use, so a missing setting can never break the admin pages themselves
    private BlobContainerClient? _container;
    private BlobContainerClient Container => _container ??= new BlobContainerClient(
        cfg["Storage:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:ConnectionString is not set."),
        "snapshots");

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var technologies = await builder.GetTechnologiesAsync(ct);
        var projects = await builder.GetProjectsAsync(ct);

        await UploadAsync("technologies.json", technologies, ct);
        await UploadAsync("projects.json", projects, ct);

        var keep = new HashSet<string>();
        foreach (var card in projects)
        {
            var detail = await builder.GetProjectAsync(card.Slug, ct);
            if (detail is null) continue;

            var name = $"project-{card.Slug}.json";
            keep.Add(name);
            await UploadAsync(name, detail, ct);
        }

        // Remove snapshots for projects that were unpublished, renamed or deleted
        await foreach (var blob in Container.GetBlobsAsync(BlobTraits.None, BlobStates.None, "project-", ct))
        {
            if (!keep.Contains(blob.Name))
                await Container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: ct);
        }
    }

    // For admin saves: a storage problem must never make an edit look like it failed. Returns false if it did.
    public async Task<bool> TryRefreshAsync()
    {
        try { await RefreshAsync(); return true; }
        catch (Exception ex) { log.LogWarning(ex, "Snapshot refresh failed"); return false; }
    }

    private Task UploadAsync<T>(string name, T data, CancellationToken ct) =>
        Container.GetBlobClient(name).UploadAsync(
            BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(data, Json)),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json",
                    CacheControl = "no-cache"
                }
            }, ct);
}