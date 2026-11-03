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
  tmdbId: number | null;
  genreIds: number[];
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
