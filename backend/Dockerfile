# Builds the API. Build context: the backend folder (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the packages stay cached as long as no project file changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/MovieTracker.Domain/MovieTracker.Domain.csproj src/MovieTracker.Domain/
COPY src/MovieTracker.Infrastructure/MovieTracker.Infrastructure.csproj src/MovieTracker.Infrastructure/
COPY src/MovieTracker.Api/MovieTracker.Api.csproj src/MovieTracker.Api/
RUN dotnet restore src/MovieTracker.Api/MovieTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/MovieTracker.Api/MovieTracker.Api.csproj --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# ASP.NET Core keeps its data protection keys (they encrypt the session cookies) in this folder.
# It is a volume in docker-compose.yml, so users stay signed in when the container is recreated.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet

COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "MovieTracker.Api.dll"]
