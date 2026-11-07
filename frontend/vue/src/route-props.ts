import type { RouteLocationNormalized } from 'vue-router';
import { queryString } from './shared/query';

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
