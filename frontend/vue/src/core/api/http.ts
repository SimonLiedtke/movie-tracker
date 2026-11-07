import { toSearchParams } from './http-params';

/**
 * A request that failed: the server answered with an error status, or (status 0) could not be
 * reached. `error` is the response body, parsed as JSON if possible.
 */
export class HttpError extends Error {
  readonly status: number;
  readonly url: string;
  readonly error: unknown;

  constructor(status: number, url: string, error: unknown) {
    super(`Http failure response for ${url}: ${status}`);
    this.name = 'HttpError';
    this.status = status;
    this.url = url;
    this.error = error;
  }
}

export interface HttpRequest {
  method: string;
  /** Including the query string. */
  url: string;
  headers: Headers;
  body: BodyInit | null;
}

export type HttpHandler = (request: HttpRequest) => Promise<unknown>;

/** Sees every request on its way to the server and every response (or error) on its way back. */
export type HttpInterceptor = (request: HttpRequest, next: HttpHandler) => Promise<unknown>;

/**
 * A thin layer over `fetch` for the JSON API: bodies are sent as JSON (a `FormData` as multipart),
 * answers are parsed as JSON (`null` if there is no body), and failures are thrown as `HttpError`.
 */
export interface Http {
  get<T>(url: string, params?: object): Promise<T>;
  post<T>(url: string, body?: unknown): Promise<T>;
  put<T>(url: string, body?: unknown): Promise<T>;
  delete<T>(url: string): Promise<T>;
  /** Adds an interceptor. The first one added sees the request first. */
  use(interceptor: HttpInterceptor): void;
}

export function createHttp(): Http {
  const interceptors: HttpInterceptor[] = [];

  function request<T>(method: string, url: string, body?: unknown, params?: object): Promise<T> {
    const query = params ? toSearchParams(params).toString() : '';
    const headers = new Headers({ Accept: 'application/json' });
    let payload: BodyInit | null = null;
    if (body instanceof FormData) {
      payload = body; // The browser sets the multipart Content-Type, with its boundary.
    } else if (body !== undefined && body !== null) {
      headers.set('Content-Type', 'application/json');
      payload = JSON.stringify(body);
    }

    const handler = interceptors.reduceRight<HttpHandler>(
      (next, interceptor) => (req) => interceptor(req, next),
      send,
    );
    return handler({
      method,
      url: query ? `${url}?${query}` : url,
      headers,
      body: payload,
    }) as Promise<T>;
  }

  return {
    get: (url, params) => request('GET', url, undefined, params),
    post: (url, body) => request('POST', url, body),
    put: (url, body) => request('PUT', url, body),
    delete: (url) => request('DELETE', url),
    use: (interceptor) => void interceptors.push(interceptor),
  };
}

async function send(request: HttpRequest): Promise<unknown> {
  let response: Response;
  try {
    response = await fetch(request.url, {
      method: request.method,
      headers: request.headers,
      body: request.body,
    });
  } catch (cause) {
    throw new HttpError(0, request.url, cause);
  }

  const body = parseBody(await response.text());
  if (!response.ok) {
    throw new HttpError(response.status, request.url, body);
  }
  return body;
}

/** JSON if it is JSON, the text if not, `null` if there is no body (e.g. 204 No Content). */
function parseBody(text: string): unknown {
  if (!text) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}
