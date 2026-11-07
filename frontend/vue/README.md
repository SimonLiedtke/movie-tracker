# Movie Tracker – Vue frontend

Vue 3 single-page app for the Movie Tracker API, styled with [Bulma](https://bulma.io/). It does the
same as the [Angular frontend](../angular/): sign in with Google, browse/search/filter movies, add and edit
movies, log viewings in the diary (rating in half stars, review, rewatch, liked), manage the watchlist, and
import a Letterboxd export.

Built with [Vite](https://vite.dev/), TypeScript, [Vue Router](https://router.vuejs.org/),
[axios](https://axios-http.com/) for the API calls, and
`<script setup>` single-file components. There is no state management library: the signed-in user and
the API clients are plain modules provided to the components with `provide`/`inject`.

## Development

Requirements: Node.js 20.19+ (22+ recommended), the backend running on http://localhost:5080.

```bash
npm install
npm start        # http://localhost:4200
```

The dev server forwards `/api` to the backend (see `vite.config.ts`), so the backend needs no CORS setup.
It uses the same port as the Angular dev server, so Google sign-in works with the same OAuth client (the
redirect URI `http://localhost:4200/api/auth/callback/google`). Do not run both at the same time: if the
port is taken, Vite picks the next one, and Google only knows the redirect URI of port 4200.

```bash
npm test         # unit tests (Vitest, Vue Test Utils)
npm run build    # type-check with vue-tsc, then production build in dist/
```

## Structure

| Folder                   | Content                                                                          |
|--------------------------|----------------------------------------------------------------------------------|
| `src/core/api`           | The axios client, typed API clients, error messages, and models mirroring the backend contracts |
| `src/core/auth`          | The signed-in user, route guards, and the axios interceptors (CSRF header, expired session) |
| `src/shared`             | Reusable components (rating, pagination, poster, movie picker) and helpers       |
| `src/features`           | Pages: sign-in, movies, diary, watchlist, Letterboxd import                      |
| `src/router.ts`          | The routes, the guards that protect them, and the page titles                    |
| `src/route-props.ts`     | How the pages get their inputs (route and query parameters) from the URL         |
| `src/create-app.ts`      | Wires everything together; `src/main.ts` starts it in the browser                |
| `src/test`               | Test helpers: a fake backend that stands in for the network (an axios adapter), and service providers   |

The tests render the pages the way the app does (in a router view) against a fake backend, so they cover
routing, the components, and the API calls together.
