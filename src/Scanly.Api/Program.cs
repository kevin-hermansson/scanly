using Azure.Identity;
using Azure.Storage.Blobs;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var blobServiceUri = builder.Configuration["Storage:BlobServiceUri"];
var containerName = builder.Configuration["Storage:ContainerName"] ?? "invoices";

if (string.IsNullOrWhiteSpace(blobServiceUri))
{
    throw new InvalidOperationException(
        "Storage:BlobServiceUri is missing from configuration."
    );
}

builder.Services.AddSingleton(
    new BlobServiceClient(
        new Uri(blobServiceUri),
        new DefaultAzureCredential()
    )
);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        service = "Scanly.Api",
        timestamp = DateTime.UtcNow
    });
})
.WithName("Health")
.WithTags("Health")
.Produces(StatusCodes.Status200OK);

app.MapGet("/invoices", async (BlobServiceClient blobServiceClient) =>
{
    var container = blobServiceClient.GetBlobContainerClient(containerName);

    var invoices = new List<InvoiceResponse>();

    await foreach (var blobItem in container.GetBlobsAsync())
    {
        var blobClient = container.GetBlobClient(blobItem.Name);

        var download = await blobClient.DownloadContentAsync();

        var invoice = download.Value.Content.ToObjectFromJson<InvoiceResponse>();

        if (invoice is not null)
        {
            invoices.Add(invoice);
        }
    }

    return Results.Ok(invoices);
})
.WithName("GetInvoices")
.WithTags("Invoices")
.Produces<List<InvoiceResponse>>(StatusCodes.Status200OK);

app.MapGet("/invoices/{id:guid}", async (
    Guid id,
    BlobServiceClient blobServiceClient) =>
{
    var container = blobServiceClient.GetBlobContainerClient(containerName);
    var blobClient = container.GetBlobClient($"{id}.json");

    if (!await blobClient.ExistsAsync())
    {
        return Results.NotFound();
    }

    var download = await blobClient.DownloadContentAsync();

    var invoice = download.Value.Content.ToObjectFromJson<InvoiceResponse>();

    return invoice is null
        ? Results.NotFound()
        : Results.Ok(invoice);
})
.WithName("GetInvoiceById")
.WithTags("Invoices")
.Produces<InvoiceResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/invoices", async (
    IFormFile file,
    BlobServiceClient blobServiceClient) =>
{
    if (file.Length == 0)
    {
        return Results.BadRequest(new
        {
            message = "Ingen fil laddades upp."
        });
    }

    var invoice = new InvoiceResponse(
        Id: Guid.NewGuid(),
        VendorName: "Ej analyserad ännu",
        InvoiceNumber: "Ej analyserad ännu",
        InvoiceDate: DateOnly.FromDateTime(DateTime.UtcNow),
        DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        Total: 0,
        Currency: "SEK"
    );

    var container = blobServiceClient.GetBlobContainerClient(containerName);

    var blobClient = container.GetBlobClient($"{invoice.Id}.json");

    var json = JsonSerializer.Serialize(invoice);

    await blobClient.UploadAsync(
        BinaryData.FromString(json),
        overwrite: true
    );

    return Results.Created($"/invoices/{invoice.Id}", invoice);
})
.WithName("CreateInvoice")
.WithTags("Invoices")
.Accepts<IFormFile>("multipart/form-data")
.DisableAntiforgery()
.Produces<InvoiceResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);

app.Run();

record InvoiceResponse(
    Guid Id,
    string VendorName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal Total,
    string Currency
);