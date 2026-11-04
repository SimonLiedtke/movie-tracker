import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';

/** Every page requires a session, except the sign-in page. */
const protectedRoutes: Routes = [
  {
    path: 'movies',
    title: 'Movies · Movie Tracker',
    loadComponent: () => import('./features/movies/movie-list').then((m) => m.MovieList),
  },
  {
    path: 'movies/new',
    title: 'Add movie · Movie Tracker',
    loadComponent: () => import('./features/movies/movie-form').then((m) => m.MovieForm),
  },
  {
    path: 'movies/:id',
    title: 'Movie · Movie Tracker',
    loadComponent: () => import('./features/movies/movie-detail').then((m) => m.MovieDetail),
  },
  {
    path: 'movies/:id/edit',
    title: 'Edit movie · Movie Tracker',
    loadComponent: () => import('./features/movies/movie-form').then((m) => m.MovieForm),
  },
  {
    path: 'diary',
    title: 'Diary · Movie Tracker',
    loadComponent: () => import('./features/diary/diary-list').then((m) => m.DiaryList),
  },
  {
    path: 'diary/new',
    title: 'Log a viewing · Movie Tracker',
    loadComponent: () => import('./features/diary/diary-entry-form').then((m) => m.DiaryEntryForm),
  },
  {
    path: 'diary/:id/edit',
    title: 'Edit diary entry · Movie Tracker',
    loadComponent: () => import('./features/diary/diary-entry-form').then((m) => m.DiaryEntryForm),
  },
  {
    path: 'watchlist',
    title: 'Watchlist · Movie Tracker',
    loadComponent: () => import('./features/watchlist/watchlist').then((m) => m.Watchlist),
  },
];

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Sign in · Movie Tracker',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  { path: '', canActivateChild: [authGuard], children: protectedRoutes },
  { path: '**', redirectTo: '' },
];
