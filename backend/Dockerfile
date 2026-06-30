# Multi-Stage-Build: SDK nur zum Bauen, Runtime-Image (deutlich kleiner,
# kein Compiler/keine Build-Tools) für den eigentlichen Container - siehe
# DEPLOYMENT.md "Produktion MVP / Variante A".
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Projektdateien zuerst kopieren und restaurieren, damit Docker dies cached
# und bei reinen Code-Änderungen (ohne geänderte .csproj) nicht erneut
# heruntergeladen werden muss.
COPY src/Dogity.Domain/Dogity.Domain.csproj src/Dogity.Domain/
COPY src/Dogity.Application/Dogity.Application.csproj src/Dogity.Application/
COPY src/Dogity.Infrastructure/Dogity.Infrastructure.csproj src/Dogity.Infrastructure/
COPY src/Dogity.Api/Dogity.Api.csproj src/Dogity.Api/
RUN dotnet restore src/Dogity.Api/Dogity.Api.csproj

COPY src/ src/
RUN dotnet publish src/Dogity.Api/Dogity.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# poppler-utils: wird vom Admin-Workflow "Prüfungsordnung-Import" über
# pdftotext aufgerufen (siehe DEPLOYMENT.md).
RUN apt-get update \
    && apt-get install -y --no-install-recommends poppler-utils \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# 0.0.0.0, da der Container sonst nur auf localhost INNERHALB des
# Containers lauschen würde - von außen (Caddy/anderen Containern im
# selben Docker-Netzwerk) nicht erreichbar.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Dogity.Api.dll"]
