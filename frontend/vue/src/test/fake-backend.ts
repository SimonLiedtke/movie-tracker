import { afterEach, beforeEach, vi } from 'vitest';

export interface FlushInit {
  status?: number;
  statusText?: string;
}

/** A request the app has sent and the test has not answered yet. */
export class PendingRequest {
  readonly method: string;
  readonly headers: Headers;
  /** The body as it was passed to `fetch`: a JSON string, a `FormData` or `null`. */
  readonly body: unknown;
  /** The URL with its query string. */
  readonly urlWithParams: string;

  constructor(
    url: string,
    init: RequestInit | undefined,
    private readonly answer: (outcome: Response | Error) => void,
  ) {
    this.urlWithParams = url;
    this.method = init?.method ?? 'GET';
    this.headers = new Headers(init?.headers);
    this.body = init?.body ?? null;
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
    this.answer(
      new Response(hasBody ? JSON.stringify(body) : null, {
        status,
        statusText,
        headers: hasBody ? { 'Content-Type': 'application/json' } : {},
      }),
    );
  }

  /** The server cannot be reached. */
  networkError(): void {
    this.answer(new TypeError('Failed to fetch'));
  }
}

type Matcher = string | ((request: PendingRequest) => boolean);

/**
 * Stands in for the network: replaces `fetch`, collects the requests, and lets the test answer them
 * (like Angular's HttpTestingController). Register it once per test file with `useFakeBackend()`.
 */
export class FakeBackend {
  private pending: PendingRequest[] = [];

  install(): void {
    this.pending = [];
    vi.stubGlobal(
      'fetch',
      (input: RequestInfo | URL, init?: RequestInit) =>
        new Promise<Response>((resolve, reject) => {
          this.pending.push(
            new PendingRequest(String(input), init, (outcome) =>
              outcome instanceof Error ? reject(outcome) : resolve(outcome),
            ),
          );
        }),
    );
  }

  uninstall(): void {
    vi.unstubAllGlobals();
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
