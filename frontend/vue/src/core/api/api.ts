import type { AxiosResponse } from 'axios';
import { inject, type InjectionKey } from 'vue';
import type { Http } from './http';
import { toSearchParams } from './http-params';
import type {
  ActivityLogEntry,
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
  PageQuery,
  PagedResponse,
  WatchlistItem,
  WatchlistPage,
  WatchlistQuery,
} from './models';

const BASE = '/api';

/** What the server answered, without the response's status and headers. */
const body = async <T>(response: Promise<AxiosResponse<T>>): Promise<T> => (await response).data;

function createGenresApi(http: Http) {
  // Genres are seeded and do not change at runtime, so one request per session is enough.
  let genres: Promise<Genre[]> | null = null;

  return {
    list(): Promise<Genre[]> {
      genres ??= body(http.get<Genre[]>(`${BASE}/genres`)).catch((error: unknown) => {
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
      return body(
        http.get<PagedResponse<MovieSummary>>(`${BASE}/movies`, { params: toSearchParams(query) }),
      );
    },

    /** Finds movies to log or watchlist: the user's manual movies and TMDB's results. */
    search(query: string): Promise<MovieSearchResponse> {
      return body(
        http.get<MovieSearchResponse>(`${BASE}/movies/search`, {
          params: toSearchParams({ query }),
        }),
      );
    },

    /** Makes a TMDB movie available for diary and watchlist. Returns the existing movie if it was imported before. */
    importFromTmdb(tmdbId: number): Promise<Movie> {
      return body(http.post<Movie>(`${BASE}/movies/tmdb/${tmdbId}`));
    },

    get(id: string): Promise<Movie> {
      return body(http.get<Movie>(`${BASE}/movies/${id}`));
    },

    /** Adds a movie manually. Only the signed-in user sees it. */
    create(request: MovieRequest): Promise<Movie> {
      return body(http.post<Movie>(`${BASE}/movies`, request));
    },

    update(id: string, request: MovieRequest): Promise<Movie> {
      return body(http.put<Movie>(`${BASE}/movies/${id}`, request));
    },

    delete(id: string): Promise<void> {
      return body(http.delete<void>(`${BASE}/movies/${id}`));
    },

    diaryEntries(id: string): Promise<DiaryEntry[]> {
      return body(http.get<DiaryEntry[]>(`${BASE}/movies/${id}/diary-entries`));
    },
  };
}

function createDiaryApi(http: Http) {
  return {
    list(query: DiaryQuery = {}): Promise<PagedResponse<DiaryEntry>> {
      return body(
        http.get<PagedResponse<DiaryEntry>>(`${BASE}/diary-entries`, {
          params: toSearchParams(query),
        }),
      );
    },

    get(id: string): Promise<DiaryEntry> {
      return body(http.get<DiaryEntry>(`${BASE}/diary-entries/${id}`));
    },

    create(request: DiaryEntryRequest): Promise<DiaryEntry> {
      return body(http.post<DiaryEntry>(`${BASE}/diary-entries`, request));
    },

    update(id: string, request: DiaryEntryRequest): Promise<DiaryEntry> {
      return body(http.put<DiaryEntry>(`${BASE}/diary-entries/${id}`, request));
    },

    delete(id: string): Promise<void> {
      return body(http.delete<void>(`${BASE}/diary-entries/${id}`));
    },
  };
}

function createWatchlistApi(http: Http) {
  return {
    list({
      genreIds,
      genreMatch,
      releasedFrom,
      releasedTo,
      addedFrom,
      addedTo,
      runtimeFrom,
      runtimeTo,
      search,
      tag,
      ...paging
    }: WatchlistQuery = {}): Promise<WatchlistPage> {
      const filtered = genreIds !== undefined && genreIds.length > 0;
      return body(
        http.get<WatchlistPage>(`${BASE}/watchlist`, {
          params: toSearchParams({
            ...paging,
            genreId: genreIds,
            genreMatch: filtered ? (genreMatch ?? 'any') : undefined,
            releasedFrom,
            releasedTo,
            addedFrom,
            addedTo,
            runtimeFrom,
            runtimeTo,
            search: search?.trim(),
            tag: tag?.trim(),
          }),
        }),
      );
    },

    /** Adds the movie to the watchlist, or replaces its note and tags if it is already on it. */
    put(movieId: string, note: string | null, tags: string[] = []): Promise<WatchlistItem> {
      return body(http.put<WatchlistItem>(`${BASE}/watchlist/${movieId}`, { note, tags }));
    },

    remove(movieId: string): Promise<void> {
      return body(http.delete<void>(`${BASE}/watchlist/${movieId}`));
    },
  };
}

function createLetterboxdImportApi(http: Http) {
  return {
    /** Uploads the ZIP archive of a Letterboxd export. The import runs on the server in the background. */
    start(file: File): Promise<ImportJob> {
      const form = new FormData();
      form.append('file', file);
      return body(http.post<ImportJob>(`${BASE}/imports/letterboxd`, form));
    },

    /** The progress of an import, or its result. 404 if the server does not know the import (any more). */
    get(id: string): Promise<ImportJob> {
      return body(http.get<ImportJob>(`${BASE}/imports/letterboxd/${id}`));
    },
  };
}

function createActivityLogApi(http: Http) {
  return {
    /** What the user did that modified the database, most recent first. */
    list(query: PageQuery = {}): Promise<PagedResponse<ActivityLogEntry>> {
      return body(
        http.get<PagedResponse<ActivityLogEntry>>(`${BASE}/activity-log`, {
          params: toSearchParams(query),
        }),
      );
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
    activityLog: createActivityLogApi(http),
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
