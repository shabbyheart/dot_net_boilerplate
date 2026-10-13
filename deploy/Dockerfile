# syntax=docker/dockerfile:1
# API image (ADR-0018). Build from the repository root:
#   docker build -f deploy/Dockerfile -t boilerplate-api .
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
WORKDIR /src

# Restore layer: only the files that affect restore, so this layer is cached until dependencies change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Boilerplate.Domain/Boilerplate.Domain.csproj src/Boilerplate.Domain/
COPY src/Boilerplate.Application/Boilerplate.Application.csproj src/Boilerplate.Application/
COPY src/Boilerplate.Infrastructure/Boilerplate.Infrastructure.csproj src/Boilerplate.Infrastructure/
COPY src/Boilerplate.Api/Boilerplate.Api.csproj src/Boilerplate.Api/
RUN dotnet restore src/Boilerplate.Api/Boilerplate.Api.csproj

# .editorconfig is needed because code style is enforced at build time.
COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Boilerplate.Api/Boilerplate.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# Chiseled: no shell or package manager. "extra" adds ICU and time zone data.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Non-root user provided by the .NET images; the API listens on 8080 (ASPNETCORE_HTTP_PORTS).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Boilerplate.Api.dll"]
