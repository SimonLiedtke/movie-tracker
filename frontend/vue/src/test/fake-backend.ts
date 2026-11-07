import axios, {
  AxiosError,
  type AxiosAdapter,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios';
import { afterEach, beforeEach, vi } from 'vitest';

export interface FlushInit {
  status?: number;
  statusText?: string;
}

/** A request the app has sent and the test has not answered yet. */
export class PendingRequest {
  readonly method: string;
  readonly headers = new Headers();
  /** The body as axios hands it to the adapter: a JSON string, a `FormData` or `null`. */
  readonly body: unknown;
  /** The URL with its query string. */
  readonly urlWithParams: string;

  constructor(
    private readonly config: InternalAxiosRequestConfig,
    private readonly answer: (outcome: AxiosResponse | AxiosError) => void,
  ) {
    this.urlWithParams = axios.getUri(config);
    this.method = (config.method ?? 'get').toUpperCase();
    for (const [name, value] of Object.entries(config.headers.toJSON(true))) {
      this.headers.set(name, String(value));
    }
    this.body = config.data ?? null;
  }

  /** The URL without the query string. */
  get url(): string {
    return this.urlWithParams.split('?')[0];
  }

  get params(): URLSearchParams {
    return new URL(this.urlWithParams, 'http://localhost').searchParams;
  }

  /** The body, parsed as JSON. */
  get json(): unknown {
    return JSON.parse(this.body as string);
  }

  /** Answers with `body` as JSON (no body for `null`/`undefined`) and the given status (200). */
  flush(body?: unknown, { status = 200, statusText = '' }: FlushInit = {}): void {
    const hasBody = body !== undefined && body !== null;
    // The raw text, like a real adapter: axios parses it after the adapter, and for failures too.
    const response: AxiosResponse = {
      data: hasBody ? JSON.stringify(body) : '',
      status,
      statusText,
      headers: hasBody ? { 'content-type': 'application/json' } : {},
      config: this.config,
      request: {},
    };
    const accepted = this.config.validateStatus ? this.config.validateStatus(status) : true;
    this.answer(
      accepted
        ? response
        : new AxiosError(
            `Request failed with status code ${status}`,
            status >= 500 ? AxiosError.ERR_BAD_RESPONSE : AxiosError.ERR_BAD_REQUEST,
            this.config,
            {},
            response,
          ),
    );
  }

  /** The server cannot be reached. */
  networkError(): void {
    this.answer(new AxiosError('Network Error', AxiosError.ERR_NETWORK, this.config, {}));
  }
}

type Matcher = string | ((request: PendingRequest) => boolean);

/**
 * Stands in for the network: replaces axios' adapter, collects the requests, and lets the test answer
 * them (like Angular's HttpTestingController). Register it once per test file with `useFakeBackend()`.
 * It applies to the clients that are created after `install()`.
 */
export class FakeBackend {
  private pending: PendingRequest[] = [];
  private adapter: typeof axios.defaults.adapter;

  install(): void {
    this.pending = [];
    this.adapter = axios.defaults.adapter;
    const fake: AxiosAdapter = (config) =>
      new Promise((resolve, reject) => {
        this.pending.push(
          new PendingRequest(config, (outcome) =>
            outcome instanceof Error ? reject(outcome) : resolve(outcome),
          ),
        );
      });
    axios.defaults.adapter = fake;
  }

  uninstall(): void {
    axios.defaults.adapter = this.adapter;
  }

  /**
   * Takes the one pending request that matches: a string is compared with the URL including the
   * query string, a function decides itself. Fails if there is none or more than one.
   */
  expectOne(match: Matcher): PendingRequest {
    const matches = this.pending.filter(matcher(match));
    if (matches.length !== 1) {
      throw new Error(
        `Expected one request matching ${describeMatcher(match)}, found ${matches.length}. ` +
          `Pending: ${this.describePending()}`,
      );
    }
    this.pending.splice(this.pending.indexOf(matches[0]), 1);
    return matches[0];
  }

  /** Like `expectOne`, but waits for the request (e.g. one that a timer sends). */
  expectOneEventually(match: Matcher): Promise<PendingRequest> {
    return vi.waitFor(() => this.expectOne(match));
  }

  /** Expects that no request is waiting for an answer. */
  verify(): void {
    if (this.pending.length) {
      throw new Error(`Unanswered requests: ${this.describePending()}`);
    }
  }

  private describePending(): string {
    return this.pending.map((r) => `${r.method} ${r.urlWithParams}`).join(', ') || '(none)';
  }
}

const matcher = (match: Matcher) => (request: PendingRequest) =>
  typeof match === 'string' ? request.urlWithParams === match : match(request);

const describeMatcher = (match: Matcher) =>
  typeof match === 'string' ? `"${match}"` : 'the predicate';

/** Call within a `describe` (or at the top of a test file): a fresh backend per test, verified after it. */
export function useFakeBackend(): FakeBackend {
  const backend = new FakeBackend();
  beforeEach(() => backend.install());
  afterEach(() => {
    try {
      backend.verify();
    } finally {
      backend.uninstall();
    }
  });
  return backend;
}
