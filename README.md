# Movie Tracker

Personal movie tracker (similar to Letterboxd, without the social features).
API-driven backend in C# with PostgreSQL, multiple frontends (Angular, Vue, React, MAUI).
There are an Angular and a Vue frontend so far; both do the same.

## Structure

| Folder      | Content                                                  |
|-------------|----------------------------------------------------------|
| `api/`      | OpenAPI specification -- the contract for all components |
| `backend/`  | ASP.NET Core API, use cases, domain model, EF Core persistence |
| `frontend/` | Frontends: `frontend/angular/` (Angular + Bulma), `frontend/vue/` (Vue + Bulma) |

## Backend layers

```
backend/src/
  MovieTracker.Domain          entities and their rules; depends on nothing
  MovieTracker.Application     use cases (services), the ports they need (repositories, TMDB) and read models
  MovieTracker.Infrastructure  PostgreSQL via EF Core, the TMDB client, the Letterboxd importer; implements the ports
  MovieTracker.Api             HTTP: controllers map requests and results, authentication, the background import worker
```

Dependencies point inwards (Api -> Application -> Domain, Infrastructure -> Application -> Domain). Only the
Infrastructure project accesses the database; controllers call use cases and never see EF Core or the database
context. `ArchitectureTests` in `MovieTracker.Api.Tests` fail if a layer reaches into another.

## Running everything with Docker

The `Dockerfile` in the repository root builds the whole app into one image: the API, which also
serves the built Angular app. `docker-compose.yml` runs it together with PostgreSQL. Only Docker is
required.

```bash
cp .env.example .env    # then fill in the Google and TMDB credentials, see below
docker compose up -d --build
```

The app is then available at http://localhost:8080. For Google sign-in, add
`http://localhost:8080/api/auth/callback/google` as an additional authorized redirect URI of the
OAuth client (see [Signing in with Google](#signing-in-with-google)). The API applies the database
migrations when it starts. The data, including the keys of the session cookies, is kept in the
`postgres-data` volume, so `docker compose down` does not lose it (`docker compose down -v` does).

After changing the code, run `docker compose up -d --build` again.

## Deploying to Render

The app runs on [Render](https://render.com) as one web service built from the `Dockerfile`, next to
a Render PostgreSQL database. `render.yaml` describes the web service.

1. Create the PostgreSQL database in the Render dashboard (if not done yet) and note its region.
2. Create the web service, either:
   - as Blueprint: **New -> Blueprint**, select this repository. Adjust `region` in `render.yaml`
     first if the database is not in Frankfurt. Or
   - by hand: **New -> Web Service**, select this repository, **Language: Docker**, same region as
     the database, and add the environment variables from `render.yaml`.
3. Fill in the secret environment variables:
   - `ConnectionStrings__MovieTracker`: the database's **Internal Database URL**
     (`postgresql://…`, under **Connect -> Internal**).
   - `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret`.
   - `Tmdb__AccessToken` (optional).
4. Add `https://<service name>.onrender.com/api/auth/callback/google` as authorized redirect URI of
   the Google OAuth client.

Every push to the connected branch deploys again; the migrations run when the app starts. The keys
of the session cookies are stored in the database, so users stay signed in across deploys.

The title search ignores accents, which uses the PostgreSQL extension `unaccent`.
A migration creates it. It is one of the standard contrib extensions (included in the
`postgres` Docker image and available on Render) and a trusted one, so the database owner may create it.

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

## Activity log

The **Activity** page of the frontend (`/activity`, `GET /api/activity-log`) lists what the signed-in user did that
modified the database, most recent first and with the time: logging, changing and deleting viewings (including
rating changes), putting movies on the watchlist, changing their notes and tags and removing them, adding, changing, importing
and deleting movies, and Letterboxd imports (one line per import). The use cases write each line in the same save
as the change itself. Saving something unchanged (e.g. the same note again) is not logged. The log keeps the movie's
title, so a line about a deleted movie still reads well. Nothing is logged for actions that happened before the log was added.

## Tags

Diary entries and watchlist items can be tagged with free text, e.g. `cinema`, `date night` or `recommended by Alice`.
In the frontends, the tags are a comma-separated field of the diary form and of the watchlist's note editor
("Edit note and tags"). A tag is shown as a small label next to its entry; clicking it lists everything with this tag
(`?tag=…` on the diary and the watchlist, where a field filters by tag as well).

- Tags are stored trimmed, in lower case, without duplicates and in alphabetical order: `Sci-Fi ` and `sci-fi` are the
  same tag. An entry has at most 20 different tags of at most 50 characters each, without commas.
- Putting a movie on the watchlist again, or editing a diary entry, replaces its tags, like its other fields.
- Logging a movie that is on the watchlist moves it to the diary, and its watchlist tags move along: the new entry gets
  them in addition to the tags that were entered when logging. Nothing gets lost, so a tag of the watchlist cannot be
  left off the entry.
- The API has `tag` as query parameter of `GET /api/diary-entries` and `GET /api/watchlist`; the diary entries and the
  watchlist items have a `tags` array, see `api/openapi.yaml`. Tag changes are in the activity log. The tags are a
  `text[]` column of `diary_entries` and `watchlist_items`.
- The Letterboxd import does not read Letterboxd's tags.

## Signing in with Google

Users sign in with their Google account. Every user has their own diary, watchlist and manually
added movies; movies from TMDB are shared. Sign-in runs on the backend (OAuth "backend for frontend"): the API handles the
Google redirect and keeps the session in an HttpOnly cookie, so the frontend never handles tokens.

One-time setup for local development:

1. In the [Google Cloud console](https://console.cloud.google.com/apis/credentials), configure the
   OAuth consent screen (while it is in "Testing" mode, add your Google account as a test user).
2. Create an OAuth client ID of type **Web application** with the authorized redirect URI
   `http://localhost:4200/api/auth/callback/google` (the Angular and the Vue dev server both use port 4200
   and forward `/api` to the backend).
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

## Importing from Letterboxd

The **Import** page of the frontend (`/import`) brings the diary and watchlist over from
[Letterboxd](https://letterboxd.com/). On Letterboxd, open **Settings -> Data -> Export your data**, then
upload the ZIP archive as it is, without unpacking it.

- What is imported: the viewings of `diary.csv` (date, rating, rewatch), their reviews (`reviews.csv`), likes
  (`likes/films.csv`; Letterboxd likes films, so all viewings of a liked film are liked here) and `watchlist.csv`
  (with the date the film was added). Nothing else in the archive, e.g. the profile, is read, and the upload
  is not stored.
- The export contains no TMDB IDs, so each film is mapped to a movie by title and release year (one year off
  is accepted): first to a movie you can use already, then to the match on TMDB, which is
  saved like any movie from the movie search. A film that TMDB does not know becomes a manual movie (private to you,
  dated to the first day of its year). Without a TMDB access token, all films become manual movies. If TMDB fails
  for a film, it is left out and reported, and not turned into a manual movie.
- It takes a while for a big diary (a few TMDB requests per film), so the import runs in the background and
  the page shows its progress. Imports are kept in memory only; after a server restart, upload the file again.
- Importing the same export again is safe: viewings (same movie, same day) and watchlist items that exist
  are skipped, and movies that are known are not looked up on TMDB again. This is also how you retry the
  films that were left out, or import a newer export.
- The API is `POST /api/imports/letterboxd` (multipart upload, answers 202) and
  `GET /api/imports/letterboxd/{importId}`, see `api/openapi.yaml`.

## Running the tests

```bash
cd backend
dotnet test
```

- `tests/MovieTracker.Domain.Tests`: unit tests of the domain model.
- `tests/MovieTracker.Application.Tests`: unit tests of the use cases and the Letterboxd export parsing, against in-memory
  fakes of the ports they need. They run in seconds and need no Docker.
- `tests/MovieTracker.Api.Tests`: integration tests of all endpoints. They start the API in memory against a
  throwaway PostgreSQL container ([Testcontainers](https://dotnet.testcontainers.org/)), so Docker must be running.
  The local development database is not touched.
- `tests/MovieTracker.TestSupport`: helpers shared by the test projects (a plain library, no tests).

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

## Vue frontend

```bash
cd frontend/vue
npm install
npm start   # http://localhost:4200, proxies /api to the backend
npm test
```

It has the same features and pages as the Angular frontend; see [frontend/vue/README.md](frontend/vue/README.md).
Run only one of the two dev servers at a time: both use port 4200, which the Google OAuth client allows.

The Docker image (see `Dockerfile`) still serves the Angular app. To serve the Vue app instead, build
`frontend/vue` in the Dockerfile's `frontend` stage (`npm ci && npm run build`) and copy its `dist` folder
to `wwwroot`.
