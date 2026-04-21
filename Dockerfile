# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MedFlow.sln .
COPY MedFlow.Core/MedFlow.Core.csproj           MedFlow.Core/
COPY MedFlow.Infrastructure/MedFlow.Infrastructure.csproj MedFlow.Infrastructure/
COPY MedFlow.Api/MedFlow.Api.csproj             MedFlow.Api/

RUN dotnet restore

COPY . .
RUN dotnet publish MedFlow.Api/MedFlow.Api.csproj -c Release -o /app/publish

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "MedFlow.Api.dll"]
