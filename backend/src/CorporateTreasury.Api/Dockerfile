# Build context is ./backend (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (better layer caching) using the project files.
COPY CorporateTreasury.sln ./
COPY src/ ./src/
RUN dotnet restore src/CorporateTreasury.Api/CorporateTreasury.Api.csproj

# Publish a release build.
RUN dotnet publish src/CorporateTreasury.Api/CorporateTreasury.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Serve plain HTTP inside the container; TLS is terminated elsewhere.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Local directory backing the named report-storage volume (no code uses it yet).
RUN mkdir -p /app/reports

ENTRYPOINT ["dotnet", "CorporateTreasury.Api.dll"]
