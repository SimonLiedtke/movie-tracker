import { flushPromises } from '@vue/test-utils';
import { afterEach } from 'vitest';
import { createMemoryHistory } from 'vue-router';
import type { User } from '../core/api/models';
import { startApp } from '../create-app';
import type { FakeBackend } from './fake-backend';

export const ALICE: User = { id: '1', email: 'alice@example.com', name: 'Alice', pictureUrl: null };

type StartedApp = Awaited<ReturnType<typeof startApp>>;

/**
 * Starts the whole app against the fake backend, at `url`, with `user` signed in (or nobody), and
 * waits until the first page is shown. Call within a `describe`: the apps are unmounted after each test.
 */
export function useTestApp(backend: FakeBackend) {
  const started: StartedApp[] = [];
  afterEach(() => {
    for (const { app } of started.splice(0)) {
      app.unmount();
    }
  });

  return async function start({ user = null as User | null, url = '/' } = {}) {
    const history = createMemoryHistory();
    history.replace(url);
    const container = document.createElement('div');
    const starting = startApp(history, container);
    const me = backend.expectOne('/api/auth/me');
    if (user) {
      me.flush(user);
    } else {
      me.flush(null, { status: 401, statusText: 'Unauthorized' });
    }
    const app = await starting;
    started.push(app);
    await flushPromises();
    return { ...app, container, text: () => container.textContent!.replace(/\s+/g, ' ') };
  };
}
