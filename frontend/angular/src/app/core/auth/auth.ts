import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { User } from '../api/models';

/**
 * The signed-in user. Signing in happens on the backend (Google OAuth), which sets an HttpOnly
 * session cookie; the frontend never handles tokens, it only asks the API who is signed in.
 */
@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly currentUser = signal<User | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly signedIn = computed(() => this.currentUser() !== null);

  /** Asks the API for the signed-in user. Called once on app start. */
  async load(): Promise<void> {
    try {
      this.currentUser.set(await firstValueFrom(this.http.get<User>('/api/auth/me')));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse && error.status === 401)) {
        console.error('Could not load the signed-in user', error);
      }
      this.currentUser.set(null);
    }
  }

  /** URL that starts the Google sign-in; the browser navigates there (no XHR). */
  loginUrl(returnUrl = '/movies'): string {
    return `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post<void>('/api/auth/logout', null));
    } finally {
      this.sessionEnded();
    }
  }

  /** The session is gone (signed out or expired): back to the sign-in page. */
  sessionEnded(returnUrl?: string): void {
    this.currentUser.set(null);
    this.router.navigate(['/'], { queryParams: returnUrl ? { returnUrl } : {} });
  }
}
