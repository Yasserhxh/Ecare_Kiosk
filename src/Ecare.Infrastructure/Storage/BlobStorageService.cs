using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

public sealed class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobService;
    private readonly BlobStorageOptions _options;
    private readonly ILogger<BlobStorageService> _log;

    public BlobStorageService(
        BlobServiceClient blobService,
        IOptions<BlobStorageOptions> options,
        ILogger<BlobStorageService> log)
    {
        _blobService = blobService;
        _options = options.Value;
        _log = log;

        if (string.IsNullOrWhiteSpace(_options.ContainerName))
            throw new InvalidOperationException("BlobStorage:ContainerName is missing.");
        if (_options.MaxFileSizeMb <= 0)
            _options.MaxFileSizeMb = 20;
        if (_options.SasExpiryMinutes <= 0)
            _options.SasExpiryMinutes = 15;
    }

    private BlobContainerClient GetContainer()
        => _blobService.GetBlobContainerClient(_options.ContainerName);

    public async Task<BlobUploadResult> UploadAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new InvalidOperationException("File is empty.");

        var maxBytes = (long)_options.MaxFileSizeMb * 1024 * 1024;
        if (file.Length > maxBytes)
            throw new InvalidOperationException($"File too large. Max: {_options.MaxFileSizeMb} MB.");

        if (!IsAllowedContentType(file.ContentType))
            throw new InvalidOperationException($"File type '{file.ContentType}' is not allowed.");

        var container = GetContainer();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var extension = Path.GetExtension(file.FileName);
        var blobName = $"{Guid.NewGuid():N}{extension}";
        var blobClient = container.GetBlobClient(blobName);

        _log.LogInformation("Uploading blob {BlobName} ({ContentType}, {Size} bytes)",
            blobName, file.ContentType, file.Length);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(
            stream,
            new BlobHttpHeaders { ContentType = file.ContentType ?? "application/octet-stream" },
            cancellationToken: ct);

        return new BlobUploadResult(
            blobName,
            file.Length,
            file.ContentType ?? "application/octet-stream");
    }

    public async Task<IReadOnlyList<BlobUploadResult>> UploadManyAsync(IFormFileCollection files, CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
            throw new InvalidOperationException("No files provided.");

        var results = new List<BlobUploadResult>(files.Count);
        foreach (var f in files)
        {
            results.Add(await UploadAsync(f, ct));
        }

        return results;
    }

    public async Task<string> GetReadSasUrlAsync(string blobName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("Blob name is required.", nameof(blobName));

        var container = GetContainer();
        var blobClient = container.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(ct))
            throw new FileNotFoundException($"Blob '{blobName}' not found.");

        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException(
                "SAS generation not supported with current credentials. Use an account key / SAS-capable connection string.");

        var sas = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_options.SasExpiryMinutes)
        };
        sas.SetPermissions(BlobSasPermissions.Read);

        var uri = blobClient.GenerateSasUri(sas);
        return uri.ToString();
    }

    private static bool IsAllowedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var allowed = new[]
        {
            "image/jpeg",
            "image/png",
            "application/pdf",
            "text/plain"
        };

        return allowed.Contains(contentType, StringComparer.OrdinalIgnoreCase);
    }
}
