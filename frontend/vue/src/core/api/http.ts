import axios, { type AxiosInstance } from 'axios';

/** The HTTP client: axios, JSON in and JSON out. A failed request is thrown as an `AxiosError`. */
export type Http = AxiosInstance;

/**
 * A client of its own, so that the interceptors added to it (see `auth.interceptor.ts`) apply to
 * this app only. Relative URLs: the dev server proxies /api to the backend (see vite.config.ts).
 */
export function createHttp(): Http {
  return axios.create();
}
