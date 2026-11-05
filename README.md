# Movie Tracker

Personal movie tracker (similar to Letterboxd, without the social features).
API-driven backend in C# with PostgreSQL, multiple frontends (Angular, Vue, React, MAUI).

## Structure

| Folder      | Content                                                  |
|-------------|----------------------------------------------------------|
| `api/`      | OpenAPI specification -- the contract for all components |
| `backend/`  | ASP.NET Core API, domain model, EF Core persistence      |
| `frontend/` | Frontends, e.g. `frontend/angular/` (Angular + Bulma)    |

## Running everything with Docker

`docker-compose.yml` runs the whole app: PostgreSQL, the API and the Angular frontend (served by
nginx, which also forwards `/api` to the API). Only Docker is required.

```bash
cp .env.example .env    # then fill in the Google and TMDB credentials, see below
docker compose up -d --build
```

The app is then available at http://localhost:8080. For Google sign-in, add
`http://localhost:8080/api/auth/callback/google` as an additional authorized redirect URI of the
OAuth client (see [Signing in with Google](#signing-in-with-google)). The API applies the database
migrations when it starts. Data and the keys of the session cookies are kept in Docker volumes, so
`docker compose down` does not lose them (`docker compose down -v` does).

After changing the code, run `docker compose up -d --build` again.

## Development

Requirements: .NET 10 SDK, Docker.

```bash
# Start only PostgreSQL
docker compose up -d postgres

# Restore the local dotnet-ef tool
cd backend
dotnet tool restore

# Run the API (applies migrations automatically in Development)
dotnet run --project src/MovieTracker.Api
```

The API listens on http://localhost:5080. The OpenAPI document generated from the code
is available at http://localhost:5080/openapi/v1.json.

## Signing in with Google

Users sign in with their Google account. Every user has their own diary, watchlist and manually
added movies; movies from TMDB are shared. Sign-in runs on the backend (OAuth "backend for frontend"): the API handles the
Google redirect and keeps the session in an HttpOnly cookie, so the frontend never handles tokens.

One-time setup for local development:

1. In the [Google Cloud console](https://console.cloud.google.com/apis/credentials), configure the
   OAuth consent screen (while it is in "Testing" mode, add your Google account as a test user).
2. Create an OAuth client ID of type **Web application** with the authorized redirect URI
   `http://localhost:4200/api/auth/callback/google` (the Angular dev server forwards `/api` to the backend).
3. Store the credentials as user secrets (they stay out of the repository):

   ```bash
   cd backend
   dotnet user-secrets set "Authentication:Google:ClientId" "<client id>" --project src/MovieTracker.Api
   dotnet user-secrets set "Authentication:Google:ClientSecret" "<client secret>" --project src/MovieTracker.Api
   ```

Without these settings the API still starts, but signing in answers with 503.

## Movie search with TMDB

Movies are found through [TMDB](https://www.themoviedb.org/) (The Movie Database). Picking a search
result saves the movie in the database, shared by all users and read-only. Movies that TMDB does not
know can be added manually; those are private to the user who added them and can be edited.

The backend calls TMDB, so the access token never reaches the browser. One-time setup:

1. Create a free account on TMDB and request an API key under
   [Settings -> API](https://www.themoviedb.org/settings/api).
2. Store the **API Read Access Token** (the long one, not the API key) as a user secret:

   ```bash
   cd backend
   dotnet user-secrets set "Tmdb:AccessToken" "<read access token>" --project src/MovieTracker.Api
   ```

Optionally, `Tmdb:Language` sets the language of titles and overviews (default `en-US`, e.g. `de-DE`).

Without a token the app still works: the search then only finds your manually added movies.
TMDB's [terms](https://www.themoviedb.org/api-terms-of-use) require the attribution shown in the
footer of the frontend.

## Running the tests

```bash
cd backend
dotnet test
```

- `tests/MovieTracker.Domain.Tests`: unit tests of the domain model.
- `tests/MovieTracker.Api.Tests`: integration tests of all endpoints. They start the API in memory against a
  throwaway PostgreSQL container ([Testcontainers](https://dotnet.testcontainers.org/)), so Docker must be running.
  The local development database is not touched.

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
