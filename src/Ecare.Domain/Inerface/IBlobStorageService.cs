
using Ecare.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;

public interface IBlobStorageService
{
    Task<BlobUploadResult> UploadAsync(IFormFile file, CancellationToken ct = default);
    Task<IReadOnlyList<BlobUploadResult>> UploadManyAsync(IFormFileCollection files, CancellationToken ct = default);

    Task<string> GetReadSasUrlAsync(string blobName, CancellationToken ct = default);
}
