import { mount, type VueWrapper } from '@vue/test-utils';
import { h, type Plugin } from 'vue';
import {
  RouterView,
  createMemoryHistory,
  createRouter,
  type RouteRecordRaw,
  type Router,
} from 'vue-router';
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

/**
 * Shows the page of a route the way the app does, in a router view, and goes to `url`. Every other
 * path is an empty page.
 */
export async function mountRoute(
  url: string,
  route: RouteRecordRaw,
  services: Parameters<typeof provideServices>[0],
): Promise<{ page: VueWrapper; router: Router }> {
  const router = createTestRouter([route, { path: '/:pathMatch(.*)*', component: EmptyPage }]);
  await router.push(url);
  const page = mount(
    { render: () => h(RouterView) },
    { global: { plugins: [router, provideServices(services)] } },
  );
  return { page, router };
}
