import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'movies' },
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
  { path: '**', redirectTo: 'movies' },
];
