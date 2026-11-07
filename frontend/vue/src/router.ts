import { createRouter, type RouteRecordRaw, type Router, type RouterHistory } from 'vue-router';
import type { Auth } from './core/auth/auth';
import { createAuthGuard, createGuestGuard } from './core/auth/auth.guard';
import {
  diaryEntryFormProps,
  diaryListProps,
  loginProps,
  movieDetailProps,
  movieFormProps,
  movieListProps,
} from './route-props';

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
      path: '/movies/new',
      name: 'movie-new',
      meta: { title: 'Add a movie manually · Movie Tracker' },
      component: () => import('./features/movies/MovieForm.vue'),
      props: movieFormProps,
    },
    {
      path: '/movies/:id',
      name: 'movie',
      meta: { title: 'Movie · Movie Tracker' },
      component: () => import('./features/movies/MovieDetail.vue'),
      props: movieDetailProps,
    },
    {
      path: '/movies/:id/edit',
      name: 'movie-edit',
      meta: { title: 'Edit movie · Movie Tracker' },
      component: () => import('./features/movies/MovieForm.vue'),
      props: movieFormProps,
    },
    {
      path: '/diary',
      name: 'diary',
      meta: { title: 'Diary · Movie Tracker' },
      component: () => import('./features/diary/DiaryList.vue'),
      props: diaryListProps,
    },
    {
      path: '/diary/new',
      name: 'diary-new',
      meta: { title: 'Log a viewing · Movie Tracker' },
      component: () => import('./features/diary/DiaryEntryForm.vue'),
      props: diaryEntryFormProps,
    },
    {
      path: '/diary/:id/edit',
      name: 'diary-edit',
      meta: { title: 'Edit diary entry · Movie Tracker' },
      component: () => import('./features/diary/DiaryEntryForm.vue'),
      props: diaryEntryFormProps,
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
