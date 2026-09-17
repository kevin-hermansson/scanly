using Azure;
using Azure.AI.DocumentIntelligence;
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

var documentIntelligenceEndpoint =
    Environment.GetEnvironmentVariable("AZURE_DI_ENDPOINT");

var documentIntelligenceKey =
    Environment.GetEnvironmentVariable("AZURE_DI_KEY");

if (!string.IsNullOrWhiteSpace(documentIntelligenceEndpoint))
{
    if (!string.IsNullOrWhiteSpace(documentIntelligenceKey))
    {
        builder.Services.AddSingleton(
            new DocumentIntelligenceClient(
                new Uri(documentIntelligenceEndpoint),
                new AzureKeyCredential(documentIntelligenceKey)
            )
        );
    }
    else
    {
        builder.Services.AddSingleton(
            new DocumentIntelligenceClient(
                new Uri(documentIntelligenceEndpoint),
                new DefaultAzureCredential()
            )
        );
    }
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        service = "Scanly.Api",
        timestamp = DateTime.UtcNow,
        documentIntelligenceConfigured =
            !string.IsNullOrWhiteSpace(documentIntelligenceEndpoint)
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
    BlobServiceClient blobServiceClient,
    IServiceProvider serviceProvider) =>
{
    if (file.Length == 0)
    {
        return Results.BadRequest(new
        {
            message = "Ingen fil laddades upp."
        });
    }

    var documentClient =
        serviceProvider.GetService<DocumentIntelligenceClient>();

    if (documentClient is null)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    await using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream);

    var fileData = BinaryData.FromBytes(memoryStream.ToArray());

    var operation = await documentClient.AnalyzeDocumentAsync(
        WaitUntil.Completed,
        "prebuilt-invoice",
        fileData
    );

    var result = operation.Value;

    if (result.Documents.Count == 0)
    {
        return Results.BadRequest(new
        {
            message = "Document Intelligence kunde inte hitta någon faktura."
        });
    }

    var document = result.Documents[0];
    var fields = document.Fields;

    var vendorName =
        GetStringField(fields, "VendorName")
        ?? "Okänd leverantör";

    var invoiceNumber =
        GetStringField(fields, "InvoiceId")
        ?? "Okänt fakturanummer";

    var invoiceDate =
        GetDateField(fields, "InvoiceDate")
        ?? DateOnly.FromDateTime(DateTime.UtcNow);

    var dueDate =
        GetDateField(fields, "DueDate")
        ?? invoiceDate.AddDays(30);

    var invoiceTotal =
        GetCurrencyAmount(fields, "InvoiceTotal")
        ?? 0m;

    var currency =
        GetCurrencyCode(fields, "InvoiceTotal")
        ?? "SEK";

    var invoice = new InvoiceResponse(
        Id: Guid.NewGuid(),
        VendorName: vendorName,
        InvoiceNumber: invoiceNumber,
        InvoiceDate: invoiceDate,
        DueDate: dueDate,
        Total: invoiceTotal,
        Currency: currency
    );

    var container =
        blobServiceClient.GetBlobContainerClient(containerName);

    var blobClient =
        container.GetBlobClient($"{invoice.Id}.json");

    var json =
        JsonSerializer.Serialize(invoice);

    await blobClient.UploadAsync(
        BinaryData.FromString(json),
        overwrite: true
    );

    return Results.Created(
        $"/invoices/{invoice.Id}",
        invoice
    );
})
.WithName("CreateInvoice")
.WithTags("Invoices")
.Accepts<IFormFile>("multipart/form-data")
.DisableAntiforgery()
.Produces<InvoiceResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status503ServiceUnavailable);

app.Run();

static string? GetStringField(
    IReadOnlyDictionary<string, DocumentField> fields,
    string fieldName)
{
    if (!fields.TryGetValue(fieldName, out var field))
    {
        return null;
    }

    return field.ValueString ?? field.Content;
}

static DateOnly? GetDateField(
    IReadOnlyDictionary<string, DocumentField> fields,
    string fieldName)
{
    if (!fields.TryGetValue(fieldName, out var field))
    {
        return null;
    }

    if (field.ValueDate is null)
    {
        return null;
    }

    return DateOnly.FromDateTime(field.ValueDate.Value.DateTime);
}

static decimal? GetCurrencyAmount(
    IReadOnlyDictionary<string, DocumentField> fields,
    string fieldName)
{
    if (!fields.TryGetValue(fieldName, out var field))
    {
        return null;
    }

    if (field.ValueCurrency is not null)
    {
        return Convert.ToDecimal(field.ValueCurrency.Amount);
    }

    if (field.ValueDouble is not null)
    {
        return Convert.ToDecimal(field.ValueDouble.Value);
    }

    return null;
}

static string? GetCurrencyCode(
    IReadOnlyDictionary<string, DocumentField> fields,
    string fieldName)
{
    if (!fields.TryGetValue(fieldName, out var field))
    {
        return null;
    }

    return field.ValueCurrency?.CurrencyCode;
}

record InvoiceResponse(
    Guid Id,
    string VendorName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal Total,
    string Currency
);