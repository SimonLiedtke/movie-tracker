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
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
}

export interface User {
  id: string;
  email: string;
  name: string;
  pictureUrl: string | null;
}
