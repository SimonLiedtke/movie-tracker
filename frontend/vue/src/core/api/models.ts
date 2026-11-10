// TypeScript mirror of the backend contracts (backend/src/MovieTracker.Api/Contracts).
// Dates without time (DateOnly) are ISO strings like "2026-10-31".

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface Genre {
  id: number;
  name: string;
}

export interface MovieRef {
  id: string;
  title: string;
  releaseDate: string | null;
  posterUrl: string | null;
}

export interface MovieSummary extends MovieRef {
  genres: Genre[];
  /** Half stars: 1 = 0.5 stars, 10 = 5 stars. */
  latestRating: number | null;
  watchCount: number;
  onWatchlist: boolean;
}

export interface Movie extends MovieSummary {
  originalTitle: string | null;
  runtimeMinutes: number | null;
  overview: string | null;
  /** Set for movies from TMDB (shared, read-only). Null for movies the user added manually (private, editable). */
  tmdbId: number | null;
  lastWatchedOn: string | null;
  createdAt: string;
}

export interface MovieRequest {
  title: string;
  originalTitle: string | null;
  releaseDate: string | null;
  runtimeMinutes: number | null;
  overview: string | null;
  posterUrl: string | null;
  genreIds: number[];
}

/**
 * A search hit: one of the user's manually added movies (movieId set, tmdbId null)
 * or a TMDB movie (tmdbId set; movieId only if it was imported before).
 */
export interface MovieSearchResult {
  movieId: string | null;
  tmdbId: number | null;
  title: string;
  originalTitle: string | null;
  releaseDate: string | null;
  posterUrl: string | null;
}

export interface MovieSearchResponse {
  items: MovieSearchResult[];
  /** False if the server has no TMDB access or TMDB is down: the items are then only manual movies. */
  tmdbAvailable: boolean;
}

export type MovieSort =
  'title' | '-title' | 'releaseDate' | '-releaseDate' | 'createdAt' | '-createdAt';

export interface MovieQuery {
  search?: string;
  genreId?: number;
  year?: number;
  sort?: MovieSort;
  page?: number;
  pageSize?: number;
}

export interface DiaryEntry {
  id: string;
  movie: MovieRef;
  watchedOn: string;
  /** Half stars: 1 = 0.5 stars, 10 = 5 stars. */
  rating: number | null;
  review: string | null;
  isRewatch: boolean;
  liked: boolean;
  createdAt: string;
}

export interface DiaryEntryRequest {
  movieId: string;
  watchedOn: string;
  rating: number | null;
  review: string | null;
  isRewatch: boolean;
  liked: boolean;
}

export interface DiaryQuery {
  from?: string;
  to?: string;
  movieId?: string;
  page?: number;
  pageSize?: number;
}

export interface WatchlistItem {
  movie: MovieRef;
  addedAt: string;
  note: string | null;
  /** The movie's genres, alphabetically. */
  genres: Genre[];
}

/** A page of the watchlist. */
export interface WatchlistPage extends PagedResponse<WatchlistItem> {
  /** The number of items on the watchlist, whatever the filter; `totalCount` counts the matching ones. */
  unfilteredCount: number;
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
}

/** `any`: at least one of the genres, `all`: every one of them. */
export type GenreMatch = 'any' | 'all';

export interface WatchlistQuery extends PageQuery {
  /** Only movies with these genres; all movies if empty. */
  genreIds?: number[];
  /** How `genreIds` are combined. Default: `any`. */
  genreMatch?: GenreMatch;
  /** Only movies released on or after this day ("YYYY-MM-DD"); movies without release date are left out. */
  releasedFrom?: string;
  /** Only movies released on or before this day ("YYYY-MM-DD"); movies without release date are left out. */
  releasedTo?: string;
}

export interface User {
  id: string;
  email: string;
  name: string;
  pictureUrl: string | null;
}

export type ImportStatus = 'running' | 'completed' | 'failed';

/** A film as Letterboxd names it. */
export interface ImportFilm {
  name: string;
  year: number | null;
}

export interface ImportResult {
  diaryEntriesImported: number;
  /** Viewings that were in the diary already (same movie, same day). */
  diaryEntriesSkipped: number;
  watchlistItemsImported: number;
  /** Films that were on the watchlist already. */
  watchlistItemsSkipped: number;
  /** Films TMDB does not know (or the server has no TMDB access): added as manual movies. */
  manualMovies: ImportFilm[];
  /** Films that could not be looked up because TMDB failed. Not imported; importing again retries them. */
  failedFilms: ImportFilm[];
  /** Rows of the export that were skipped, and why. */
  warnings: string[];
}

export interface ImportJob {
  id: string;
  status: ImportStatus;
  /** The number of different films in the diary and the watchlist; 0 until the import has started. */
  filmsTotal: number;
  filmsProcessed: number;
  result: ImportResult | null;
  error: string | null;
}
