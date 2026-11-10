import type { RouteLocationNormalized } from 'vue-router';
import { queryString, queryStrings } from './shared/query';

// How the pages get their inputs from the URL (like Angular's component input binding): route and
// query parameters become props. Missing parameters are `undefined`.

/** Where to go after signing in, and whether the last attempt failed. */
export const loginProps = (route: RouteLocationNormalized) => ({
  returnUrl: queryString(route.query['returnUrl']),
  loginError: queryString(route.query['loginError']),
});

/** The filters of the movie list. */
export const movieListProps = (route: RouteLocationNormalized) => ({
  search: queryString(route.query['search']),
  genreId: queryString(route.query['genreId']),
  year: queryString(route.query['year']),
  sort: queryString(route.query['sort']),
  page: queryString(route.query['page']),
});

/** The movie to show. */
export const movieDetailProps = (route: RouteLocationNormalized) => ({
  id: route.params['id'] as string,
});

/** The movie to edit (none: add a movie), a title to start with, and where to continue after adding. */
export const movieFormProps = (route: RouteLocationNormalized) => ({
  id: route.params['id'] as string | undefined,
  title: queryString(route.query['title']),
  next: queryString(route.query['next']),
});

/** The period and page of the diary. */
export const diaryListProps = (route: RouteLocationNormalized) => ({
  from: queryString(route.query['from']),
  to: queryString(route.query['to']),
  page: queryString(route.query['page']),
});

/** The diary entry to edit (none: log a viewing), and a movie to preselect. */
export const diaryEntryFormProps = (route: RouteLocationNormalized) => ({
  id: route.params['id'] as string | undefined,
  movieId: queryString(route.query['movieId']),
});

/** The filters of the watchlist (genre IDs, any or all of them, release period, period added) and its page. */
export const watchlistProps = (route: RouteLocationNormalized) => ({
  genreId: queryStrings(route.query['genreId']),
  genreMatch: queryString(route.query['genreMatch']),
  releasedFrom: queryString(route.query['releasedFrom']),
  releasedTo: queryString(route.query['releasedTo']),
  addedFrom: queryString(route.query['addedFrom']),
  addedTo: queryString(route.query['addedTo']),
  page: queryString(route.query['page']),
});

/** The import to show (none: the upload form). */
export const letterboxdImportProps = (route: RouteLocationNormalized) => ({
  jobId: queryString(route.query['job']),
});
