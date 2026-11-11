import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { toHttpParams } from './http-params';
import {
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

// Relative URLs: the dev server proxies /api to the backend (see proxy.conf.json).
const BASE = '/api';

@Injectable({ providedIn: 'root' })
export class GenresApi {
  private readonly http = inject(HttpClient);

  // Genres are seeded and do not change at runtime, so one request per session is enough.
  private readonly genres$ = this.http.get<Genre[]>(`${BASE}/genres`).pipe(shareReplay(1));

  list(): Observable<Genre[]> {
    return this.genres$;
  }
}

@Injectable({ providedIn: 'root' })
export class MoviesApi {
  private readonly http = inject(HttpClient);

  /** The user's movies: added manually, logged or on the watchlist. */
  list(query: MovieQuery = {}): Observable<PagedResponse<MovieSummary>> {
    return this.http.get<PagedResponse<MovieSummary>>(`${BASE}/movies`, {
      params: toHttpParams(query),
    });
  }

  /** Finds movies to log or watchlist: the user's manual movies and TMDB's results. */
  search(query: string): Observable<MovieSearchResponse> {
    return this.http.get<MovieSearchResponse>(`${BASE}/movies/search`, { params: { query } });
  }

  /** Makes a TMDB movie available for diary and watchlist. Returns the existing movie if it was imported before. */
  importFromTmdb(tmdbId: number): Observable<Movie> {
    return this.http.post<Movie>(`${BASE}/movies/tmdb/${tmdbId}`, null);
  }

  get(id: string): Observable<Movie> {
    return this.http.get<Movie>(`${BASE}/movies/${id}`);
  }

  /** Adds a movie manually. Only the signed-in user sees it. */
  create(request: MovieRequest): Observable<Movie> {
    return this.http.post<Movie>(`${BASE}/movies`, request);
  }

  update(id: string, request: MovieRequest): Observable<Movie> {
    return this.http.put<Movie>(`${BASE}/movies/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/movies/${id}`);
  }

  diaryEntries(id: string): Observable<DiaryEntry[]> {
    return this.http.get<DiaryEntry[]>(`${BASE}/movies/${id}/diary-entries`);
  }
}

@Injectable({ providedIn: 'root' })
export class DiaryApi {
  private readonly http = inject(HttpClient);

  list(query: DiaryQuery = {}): Observable<PagedResponse<DiaryEntry>> {
    return this.http.get<PagedResponse<DiaryEntry>>(`${BASE}/diary-entries`, {
      params: toHttpParams(query),
    });
  }

  get(id: string): Observable<DiaryEntry> {
    return this.http.get<DiaryEntry>(`${BASE}/diary-entries/${id}`);
  }

  create(request: DiaryEntryRequest): Observable<DiaryEntry> {
    return this.http.post<DiaryEntry>(`${BASE}/diary-entries`, request);
  }

  update(id: string, request: DiaryEntryRequest): Observable<DiaryEntry> {
    return this.http.put<DiaryEntry>(`${BASE}/diary-entries/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/diary-entries/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class WatchlistApi {
  private readonly http = inject(HttpClient);

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
  }: WatchlistQuery = {}): Observable<WatchlistPage> {
    const filtered = genreIds !== undefined && genreIds.length > 0;
    return this.http.get<WatchlistPage>(`${BASE}/watchlist`, {
      params: toHttpParams({
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
    });
  }

  /** Adds the movie to the watchlist, or replaces its note and tags if it is already on it. */
  put(movieId: string, note: string | null, tags: string[] = []): Observable<WatchlistItem> {
    return this.http.put<WatchlistItem>(`${BASE}/watchlist/${movieId}`, { note, tags });
  }

  remove(movieId: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/watchlist/${movieId}`);
  }
}

@Injectable({ providedIn: 'root' })
export class LetterboxdImportApi {
  private readonly http = inject(HttpClient);

  /** Uploads the ZIP archive of a Letterboxd export. The import runs on the server in the background. */
  start(file: File): Observable<ImportJob> {
    const body = new FormData();
    body.append('file', file);
    return this.http.post<ImportJob>(`${BASE}/imports/letterboxd`, body);
  }

  /** The progress of an import, or its result. 404 if the server does not know the import (any more). */
  get(id: string): Observable<ImportJob> {
    return this.http.get<ImportJob>(`${BASE}/imports/letterboxd/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class ActivityLogApi {
  private readonly http = inject(HttpClient);

  /** What the user did that modified the database, most recent first. */
  list(query: PageQuery = {}): Observable<PagedResponse<ActivityLogEntry>> {
    return this.http.get<PagedResponse<ActivityLogEntry>>(`${BASE}/activity-log`, {
      params: toHttpParams(query),
    });
  }
}
