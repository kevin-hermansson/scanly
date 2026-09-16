FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Scanly.slnx ./
COPY src/Scanly.Api/Scanly.Api.csproj src/Scanly.Api/
COPY tests/Scanly.Api.Tests/Scanly.Api.Tests.csproj tests/Scanly.Api.Tests/

RUN dotnet restore src/Scanly.Api/Scanly.Api.csproj

COPY . .

RUN dotnet publish src/Scanly.Api/Scanly.Api.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "Scanly.Api.dll"]