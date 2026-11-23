# Movie Tracker – Blazor frontend

[Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) single-page app for the Movie Tracker API, styled
with [Bulma](https://bulma.io/). It does the same as the [Angular](../angular/) and [Vue](../vue/) frontends: sign in
with Google, browse/search/filter movies, add and edit movies (with a poster from an image file or a URL), log viewings
in the diary (rating in half stars, review, tags, rewatch, liked), manage the watchlist (filters, notes, tags), see the
activity log, and import a Letterboxd export.

The app runs in the browser (WebAssembly), as C# and Razor components, and talks to the API with `HttpClient`.
Like the other frontends, it never handles tokens: signing in happens on the backend, which sets an HttpOnly session
cookie. There is no state management library: the signed-in user (`AuthService`) and the API clients (`Api`) are
services of the dependency injection container.

## Development

Requirements: the .NET 10 SDK, the backend running on http://localhost:5080.

```bash
dotnet run --project src/MovieTracker.Blazor.DevServer   # http://localhost:4200
```

Blazor's own development server cannot forward requests, so `MovieTracker.Blazor.DevServer` is a small ASP.NET Core app
that serves the Blazor app and forwards `/api` to the backend ([YARP](https://microsoft.github.io/reverse-proxy/)): the
backend needs no CORS setup, and the session cookie works. `Backend:Url` (default `http://localhost:5080`) says where the
backend is, e.g. `dotnet run --project src/MovieTracker.Blazor.DevServer --Backend:Url=http://localhost:5081`.
`dotnet watch --project src/MovieTracker.Blazor.DevServer` rebuilds when a file changes.

It uses the same port as the Angular and Vue dev servers, so Google sign-in works with the same OAuth client (the redirect
URI `http://localhost:4200/api/auth/callback/google`). Do not run two of them at the same time.

```bash
dotnet test      # unit tests and component tests (xUnit, bUnit)
dotnet build     # the whole solution
dotnet publish src/MovieTracker.Blazor -c Release -o publish   # static files in publish/wwwroot
```

## Structure

| Folder                                      | Content                                                                              |
|---------------------------------------------|--------------------------------------------------------------------------------------|
| `src/MovieTracker.Blazor/Core/Api`          | The typed API clients, error messages, and models mirroring the backend contracts    |
| `src/MovieTracker.Blazor/Core/Auth`         | The signed-in user, and the HTTP handler (CSRF header, expired session)              |
| `src/MovieTracker.Blazor/Core`              | Helpers: dates, ratings, tags, query parameters                                      |
| `src/MovieTracker.Blazor/Shared`            | Reusable components (rating, pagination, poster, movie picker, tag list)             |
| `src/MovieTracker.Blazor/Features`          | Pages: sign-in, movies, diary, watchlist, activity log, Letterboxd import            |
| `src/MovieTracker.Blazor/Layout`            | The navigation bar and footer, and the guard that sends visitors without a session to the sign-in page |
| `src/MovieTracker.Blazor/wwwroot`           | `index.html`, the stylesheet (`bulma.min.css`, vendored) and the icons               |
| `src/MovieTracker.Blazor.DevServer`         | The development server (see above)                                                   |
| `tests/MovieTracker.Blazor.Tests`           | The tests                                                                            |

The pages are driven by the URL, as in the other frontends: filters and the page are query parameters
(`/diary?tag=cinema&page=2`), read with `[SupplyParameterFromQuery]`, and changing a filter navigates. So every state can
be bookmarked, and the back button works.

The tests render the app the way it runs (the router with all pages) against a fake backend that stands in for the
network, so they cover routing, the components, and the API calls together.

## Serving it from the backend

The API serves static files from its `wwwroot` folder, as it does for the Angular app. `dotnet publish` produces plain
static files, so the Blazor app can be served the same way. The `Dockerfile` in the repository root still serves the Angular
app; to serve the Blazor app instead, replace its `frontend` stage with

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS frontend
WORKDIR /src
COPY frontend/blazor/Directory.Build.props ./
COPY frontend/blazor/src/MovieTracker.Blazor/ src/MovieTracker.Blazor/
RUN dotnet publish src/MovieTracker.Blazor/MovieTracker.Blazor.csproj -c Release -o /out
```

and copy `/out/wwwroot` instead of `/src/dist/angular/browser` into the image's `wwwroot`.

The download is about 6 MB of WebAssembly, JavaScript and .NET libraries (the first visit). The published files come with
Brotli and gzip versions next to them; the API serves the plain ones.
