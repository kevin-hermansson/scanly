# Scanly

Scanly är ett API för att analysera fakturor med Azure Document Intelligence.

En användare kan ladda upp en faktura som PDF eller bild och få tillbaka strukturerad information som till exempel:

- leverantör
- fakturanummer
- fakturadatum
- förfallodatum
- totalbelopp
- valuta
- radposter

## Teknik

Projektet använder bland annat:

- .NET 10
- Azure Container Apps
- Azure Container Registry
- Azure Blob Storage
- Azure Document Intelligence
- Bicep
- Docker
- Azure DevOps Pipelines
- Swagger

## API

Följande endpoints finns:

- `POST /invoices`
- `GET /invoices/{id}`
- `GET /invoices`
- `GET /health`

Swagger finns här:

`https://scanly-app.braveocean-2c55d0cc.swedencentral.azurecontainerapps.io/swagger`

## Köra lokalt

Bygg projektet:

```bash
dotnet build