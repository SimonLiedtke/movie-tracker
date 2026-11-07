import { createRouter, type RouteRecordRaw, type Router, type RouterHistory } from 'vue-router';
import type { Auth } from './core/auth/auth';
import { createAuthGuard, createGuestGuard } from './core/auth/auth.guard';
import { loginProps, movieDetailProps, movieListProps } from './route-props';

declare module 'vue-router' {
  interface RouteMeta {
    /** The page title, shown in the browser's tab. */
    title?: string;
  }
}

export function createAppRouter(auth: Auth, history: RouterHistory): Router {
  const authGuard = createAuthGuard(auth);
  const guestGuard = createGuestGuard(auth);

  // Names tell the app when a page is a different one (its state is thrown away) and when it is
  // the same page with other parameters (it stays and reacts).
  const pages: RouteRecordRaw[] = [
    {
      path: '/movies',
      name: 'movies',
      meta: { title: 'My movies · Movie Tracker' },
      component: () => import('./features/movies/MovieList.vue'),
      props: movieListProps,
    },
    {
      path: '/movies/:id',
      name: 'movie',
      meta: { title: 'Movie · Movie Tracker' },
      component: () => import('./features/movies/MovieDetail.vue'),
      props: movieDetailProps,
    },
  ];

  const router = createRouter({
    history,
    routes: [
      {
        path: '/',
        name: 'login',
        meta: { title: 'Sign in · Movie Tracker' },
        beforeEnter: guestGuard,
        component: () => import('./features/login/Login.vue'),
        props: loginProps,
      },
      // Every page requires a session, except the sign-in page.
      ...pages.map((page): RouteRecordRaw => ({ ...page, beforeEnter: authGuard })),
      { path: '/:pathMatch(.*)*', redirect: '/' },
    ],
  });

  router.afterEach((to) => {
    if (to.meta.title) {
      document.title = to.meta.title;
    }
  });

  return router;
}
