# The whole app in one image: the API, which also serves the built Angular app.
# Used by docker-compose.yml and by Render (render.yaml). Build context: the repository root.

FROM node:22-alpine AS frontend
WORKDIR /src
COPY frontend/angular/package.json frontend/angular/package-lock.json ./
RUN npm ci
COPY frontend/angular/ ./
RUN npx ng build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src

# Restore first, so the packages stay cached as long as no project file changes.
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/MovieTracker.Domain/MovieTracker.Domain.csproj src/MovieTracker.Domain/
COPY backend/src/MovieTracker.Application/MovieTracker.Application.csproj src/MovieTracker.Application/
COPY backend/src/MovieTracker.Infrastructure/MovieTracker.Infrastructure.csproj src/MovieTracker.Infrastructure/
COPY backend/src/MovieTracker.Api/MovieTracker.Api.csproj src/MovieTracker.Api/
RUN dotnet restore src/MovieTracker.Api/MovieTracker.Api.csproj

COPY backend/src/ src/
RUN dotnet publish src/MovieTracker.Api/MovieTracker.Api.csproj --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app .
COPY --from=frontend /src/dist/angular/browser ./wwwroot
USER app
# The image's default; Render routes to it through the PORT variable in render.yaml.
EXPOSE 8080
ENTRYPOINT ["dotnet", "MovieTracker.Api.dll"]
