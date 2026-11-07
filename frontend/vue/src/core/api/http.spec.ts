import { describe, expect, it } from 'vitest';
import { useFakeBackend } from '../../test/fake-backend';
import { createHttp } from './http';

describe('createHttp', () => {
  const backend = useFakeBackend();

  it('gives a client of its own, so that the interceptors of one do not touch another', async () => {
    const first = createHttp();
    const second = createHttp();
    first.interceptors.request.use(
      (config) => {
        config.headers.set('X-First', '1');
        return config;
      },
      null,
      { synchronous: true },
    );

    const firstAnswer = first.get('/api/genres');
    const secondAnswer = second.get('/api/movies');

    const firstRequest = backend.expectOne('/api/genres');
    const secondRequest = backend.expectOne('/api/movies');
    expect(firstRequest.headers.get('X-First')).toBe('1');
    expect(secondRequest.headers.has('X-First')).toBe(false);
    firstRequest.flush([]);
    secondRequest.flush([]);
    await Promise.all([firstAnswer, secondAnswer]);
  });
});
