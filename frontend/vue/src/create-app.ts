import { createApp } from 'vue';
import type { RouterHistory } from 'vue-router';
import App from './App.vue';
import { apiKey, createApi } from './core/api/api';
import { createHttp } from './core/api/http';
import { authKey, createAuth } from './core/auth/auth';
import { createAuthInterceptor } from './core/auth/auth.interceptor';
import { createAppRouter } from './router';

/** Builds the app and shows it in `container`, once it is known whether someone is signed in. */
export async function startApp(history: RouterHistory, container: string | Element) {
  const http = createHttp();
  const api = createApi(http);
  const auth = createAuth({ http, navigate: (to) => router.push(to) });
  const router = createAppRouter(auth, history);
  http.use(
    createAuthInterceptor({
      sessionEnded: (returnUrl) => auth.sessionEnded(returnUrl),
      currentUrl: () => router.currentRoute.value.fullPath,
    }),
  );

  // The guards need to know whether someone is signed in before the first navigation, which
  // starts when the router is installed.
  await auth.load();

  const app = createApp(App).provide(apiKey, api).provide(authKey, auth).use(router);
  app.mount(container);
  await router.isReady();
  return { app, router, auth };
}
