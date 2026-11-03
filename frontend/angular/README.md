# Movie Tracker – Angular frontend

Angular 21 single-page app for the Movie Tracker API, styled with [Bulma](https://bulma.io/).

Features: browse/search/filter movies, add and edit movies, log viewings in the diary
(rating in half stars, review, rewatch, liked), and manage the watchlist.

## Development

Requirements: Node.js 20.19+ (Angular 22 would need Node 22+), the backend running on http://localhost:5080.

```bash
npm install
npm start        # http://localhost:4200
```

The dev server forwards `/api` to the backend (see `proxy.conf.json`), so the backend needs no CORS setup.

```bash
npm test         # unit tests (Vitest)
npm run build    # production build in dist/
```

## Structure

| Folder              | Content                                                   |
|---------------------|-----------------------------------------------------------|
| `src/app/core/api`  | Typed API clients and models mirroring the backend contracts |
| `src/app/shared`    | Reusable components (rating, pagination, poster, movie picker) |
| `src/app/features`  | Pages: movies, diary, watchlist                           |
