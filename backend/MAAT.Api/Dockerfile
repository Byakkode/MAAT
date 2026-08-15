# syntax=docker/dockerfile:1
#
# Contexte de build attendu : backend/ (docker-compose.prod.yml définit
# build.context: ./backend, build.dockerfile: MAAT.Api/Dockerfile) — pas la racine du
# dépôt, pour ne pas envoyer frontend/ ni docs/ au démon à chaque build.
#
# Trois cibles :
#   runtime  (défaut) — image de service, utilisateur non-root, pas de SDK.
#   migrator            — applique les migrations EF Core (docs/specs/deploiement.md,
#                          section 6 : jamais automatique au démarrage de "runtime").
# Les deux partagent l'étape "build" ci-dessous.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY Directory.Packages.props .
COPY MAAT.Domain/MAAT.Domain.csproj MAAT.Domain/
COPY MAAT.Application/MAAT.Application.csproj MAAT.Application/
COPY MAAT.Infrastructure/MAAT.Infrastructure.csproj MAAT.Infrastructure/
COPY MAAT.Api/MAAT.Api.csproj MAAT.Api/
RUN dotnet restore MAAT.Api/MAAT.Api.csproj

COPY MAAT.Domain/ MAAT.Domain/
COPY MAAT.Application/ MAAT.Application/
COPY MAAT.Infrastructure/ MAAT.Infrastructure/
COPY MAAT.Api/ MAAT.Api/
RUN dotnet publish MAAT.Api/MAAT.Api.csproj -c Release -o /app --no-restore

# ---------------------------------------------------------------------------
FROM build AS migrator

# Version du outil alignée sur Microsoft.EntityFrameworkCore.Design
# (backend/Directory.Packages.props) : un dotnet-ef plus récent que le paquet Design
# référencé par le projet refuse de s'exécuter.
RUN dotnet tool install --global dotnet-ef --version 10.0.10
ENV PATH="$PATH:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "MAAT.Infrastructure", "--startup-project", "MAAT.Api"]

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# docs/specs/rapport-pdf.md, section 5 : SkiaSharp.NativeAssets.Linux charge
# libSkiaSharp.so contre fontconfig au premier rapport généré — absent de l'image
# aspnet nue (docs/specs/deploiement.md, section 9 : l'échec est une exception au
# runtime, pas à la compilation). curl : utilisé par HEALTHCHECK ci-dessous et par
# deploy.sh pour interroger /api/health depuis l'intérieur du conteneur.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 curl \
    && rm -rf /var/lib/apt/lists/*

# docs/specs/deploiement.md, section 5 : utilisateur de service dédié, sans droits
# d'administration — appliqué ici au niveau du conteneur, en plus de l'utilisateur
# système équivalent côté hôte (voir docs/deploiement/checklist.md).
RUN useradd --system --create-home --shell /usr/sbin/nologin maat

COPY --from=build --chown=maat:maat /app .

USER maat

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fs http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "MAAT.Api.dll"]
