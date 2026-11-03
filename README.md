# Movie Tracker

Personal movie tracker (similar to Letterboxd, without the social features).
API-driven backend in C# with PostgreSQL, multiple frontends (Angular, Vue, React, MAUI).

## Structure

| Folder      | Content                                                  |
|-------------|----------------------------------------------------------|
| `api/`      | OpenAPI specification -- the contract for all components |
| `backend/`  | ASP.NET Core API, domain model, EF Core persistence      |
| `frontend/` | Frontends, e.g. `frontend/angular/` (Angular + Bulma)    |

## Getting started

Requirements: .NET 10 SDK, Docker.

```bash
# Start PostgreSQL
docker compose up -d

# Restore the local dotnet-ef tool
cd backend
dotnet tool restore

# Run the API (applies migrations automatically in Development)
dotnet run --project src/MovieTracker.Api
```

The API listens on http://localhost:5080. The OpenAPI document generated from the code
is available at http://localhost:5080/openapi/v1.json.

## Adding a migration

```bash
cd backend
dotnet ef migrations add <Name> \
  --project src/MovieTracker.Infrastructure \
  --startup-project src/MovieTracker.Api \
  --output-dir Persistence/Migrations
```

## Angular frontend

```bash
cd frontend/angular
npm install
npm start   # http://localhost:4200, proxies /api to the backend
```
