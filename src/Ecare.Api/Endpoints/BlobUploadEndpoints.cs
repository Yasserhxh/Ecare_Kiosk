using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecare.Api.Endpoints;

public static class BlobEndpoints
{
    public static IEndpointRouteBuilder MapBlobEndpoints(this IEndpointRouteBuilder app)
    {
        // /files group, all secured
        var group = app.MapGroup("/files")
            .WithTags("Files");


        // 1) Upload single or multiple
        group.MapPost("/upload",
    async (
        HttpRequest request,
        [FromServices] IBlobStorageService storage,
        ILoggerFactory loggerFactory,
        CancellationToken ct) =>
    {
        var log = loggerFactory.CreateLogger("Files.Upload");

        if (!request.HasFormContentType)
            return Results.BadRequest("Request must be multipart/form-data");

        var form = await request.ReadFormAsync(ct);

        if (form.Files.Count == 0)
            return Results.BadRequest("No files received");

        // Single
        if (form.Files.Count == 1)
        {
            var r = await storage.UploadAsync(form.Files[0], ct);
            log.LogInformation("Uploaded blob: {BlobName}", r.BlobName);
            return Results.Ok(new
            {
                fileName = r.BlobName,
                size = r.Size,
                contentType = r.ContentType
            });
        }

        // Multiple
        var results = new List<object>();
        foreach (var file in form.Files)
        {
            var r = await storage.UploadAsync(file, ct);
            results.Add(new
            {
                fileName = r.BlobName,
                size = r.Size,
                contentType = r.ContentType
            });
        }

        log.LogInformation("Uploaded {Count} blobs", results.Count);
        return Results.Ok(results);
    })
    .DisableAntiforgery()
    .WithName("UploadFiles");


        // 2) Get short-lived SAS URL for a blob
        group.MapGet("/{blobName}/sas",
     async (
         [FromRoute] string blobName,
         [FromServices] IBlobStorageService storage,
         ILoggerFactory loggerFactory,
         CancellationToken ct) =>
     {
         var log = loggerFactory.CreateLogger("Files.GetSas");
         if (string.IsNullOrWhiteSpace(blobName))
             return Results.BadRequest("Blob name is required");

         try
         {
             var url = await storage.GetReadSasUrlAsync(blobName, ct);
             log.LogInformation("Generated SAS for {BlobName}", blobName);
             return Results.Ok(new { blobName, url });
         }
         catch (FileNotFoundException)
         {
             return Results.NotFound("Blob not found");
         }
         catch (Exception ex)
         {
             log.LogError(ex, "Error generating SAS for {BlobName}", blobName);
             return Results.Problem("Could not generate SAS URL");
         }
     })
     .WithName("GetBlobSasUrl");


        
             

        return app;
    }
}
