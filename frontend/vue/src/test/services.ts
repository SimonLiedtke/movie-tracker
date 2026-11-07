import type { Plugin } from 'vue';
import { createMemoryHistory, createRouter, type RouteRecordRaw, type Router } from 'vue-router';
import { apiKey, type Api } from '../core/api/api';
import { authKey, type Auth } from '../core/auth/auth';

/** A page that shows nothing, for tests of components that only need routes to link to. */
export const EmptyPage = { render: () => null };

/** A router for tests: in memory, and with the given routes (by default, every path is a route). */
export function createTestRouter(
  routes: RouteRecordRaw[] = [{ path: '/:pathMatch(.*)*', component: EmptyPage }],
): Router {
  return createRouter({ history: createMemoryHistory(), routes });
}

/** Provides services to the components of a test, like a TestBed's providers. */
export function provideServices(services: { api?: Api; auth?: Partial<Auth> }): Plugin {
  return (app) => {
    if (services.api) {
      app.provide(apiKey, services.api);
    }
    if (services.auth) {
      app.provide(authKey, services.auth as Auth);
    }
  };
}
