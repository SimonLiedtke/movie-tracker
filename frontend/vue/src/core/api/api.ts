import { inject, type InjectionKey } from 'vue';
import type { Http } from './http';
import type {
  DiaryEntry,
  DiaryEntryRequest,
  DiaryQuery,
  Genre,
  ImportJob,
  Movie,
  MovieQuery,
  MovieRequest,
  MovieSearchResponse,
  MovieSummary,
  PagedResponse,
  PageQuery,
  WatchlistItem,
} from './models';

// Relative URLs: the dev server proxies /api to the backend (see vite.config.ts).
const BASE = '/api';

function createGenresApi(http: Http) {
  // Genres are seeded and do not change at runtime, so one request per session is enough.
  let genres: Promise<Genre[]> | null = null;

  return {
    list(): Promise<Genre[]> {
      genres ??= http.get<Genre[]>(`${BASE}/genres`).catch((error: unknown) => {
        genres = null; // Try again the next time.
        throw error;
      });
      return genres;
    },
  };
}

function createMoviesApi(http: Http) {
  return {
    /** The user's movies: added manually, logged or on the watchlist. */
    list(query: MovieQuery = {}): Promise<PagedResponse<MovieSummary>> {
      return http.get(`${BASE}/movies`, query);
    },

    /** Finds movies to log or watchlist: the user's manual movies and TMDB's results. */
    search(query: string): Promise<MovieSearchResponse> {
      return http.get(`${BASE}/movies/search`, { query });
    },

    /** Makes a TMDB movie available for diary and watchlist. Returns the existing movie if it was imported before. */
    importFromTmdb(tmdbId: number): Promise<Movie> {
      return http.post(`${BASE}/movies/tmdb/${tmdbId}`, null);
    },

    get(id: string): Promise<Movie> {
      return http.get(`${BASE}/movies/${id}`);
    },

    /** Adds a movie manually. Only the signed-in user sees it. */
    create(request: MovieRequest): Promise<Movie> {
      return http.post(`${BASE}/movies`, request);
    },

    update(id: string, request: MovieRequest): Promise<Movie> {
      return http.put(`${BASE}/movies/${id}`, request);
    },

    delete(id: string): Promise<void> {
      return http.delete(`${BASE}/movies/${id}`);
    },

    diaryEntries(id: string): Promise<DiaryEntry[]> {
      return http.get(`${BASE}/movies/${id}/diary-entries`);
    },
  };
}

function createDiaryApi(http: Http) {
  return {
    list(query: DiaryQuery = {}): Promise<PagedResponse<DiaryEntry>> {
      return http.get(`${BASE}/diary-entries`, query);
    },

    get(id: string): Promise<DiaryEntry> {
      return http.get(`${BASE}/diary-entries/${id}`);
    },

    create(request: DiaryEntryRequest): Promise<DiaryEntry> {
      return http.post(`${BASE}/diary-entries`, request);
    },

    update(id: string, request: DiaryEntryRequest): Promise<DiaryEntry> {
      return http.put(`${BASE}/diary-entries/${id}`, request);
    },

    delete(id: string): Promise<void> {
      return http.delete(`${BASE}/diary-entries/${id}`);
    },
  };
}

function createWatchlistApi(http: Http) {
  return {
    list(query: PageQuery = {}): Promise<PagedResponse<WatchlistItem>> {
      return http.get(`${BASE}/watchlist`, query);
    },

    /** Adds the movie to the watchlist, or updates the note if it is already on it. */
    put(movieId: string, note: string | null): Promise<WatchlistItem> {
      return http.put(`${BASE}/watchlist/${movieId}`, { note });
    },

    remove(movieId: string): Promise<void> {
      return http.delete(`${BASE}/watchlist/${movieId}`);
    },
  };
}

function createLetterboxdImportApi(http: Http) {
  return {
    /** Uploads the ZIP archive of a Letterboxd export. The import runs on the server in the background. */
    start(file: File): Promise<ImportJob> {
      const body = new FormData();
      body.append('file', file);
      return http.post(`${BASE}/imports/letterboxd`, body);
    },

    /** The progress of an import, or its result. 404 if the server does not know the import (any more). */
    get(id: string): Promise<ImportJob> {
      return http.get(`${BASE}/imports/letterboxd/${id}`);
    },
  };
}

/** Typed clients for the API's endpoints. */
export function createApi(http: Http) {
  return {
    genres: createGenresApi(http),
    movies: createMoviesApi(http),
    diary: createDiaryApi(http),
    watchlist: createWatchlistApi(http),
    letterboxdImport: createLetterboxdImportApi(http),
  };
}

export type Api = ReturnType<typeof createApi>;

export const apiKey: InjectionKey<Api> = Symbol('api');

/** The API clients, for components (provided by the app, see `createMovieTrackerApp`). */
export function useApi(): Api {
  const api = inject(apiKey);
  if (!api) {
    throw new Error('The API clients are not provided.');
  }
  return api;
}
