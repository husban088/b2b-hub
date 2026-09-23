import { Injectable, signal } from '@angular/core';
import { Apollo } from 'apollo-angular';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LOGIN } from '../graphql.operations';
import { AppUser, AuthPayload } from '../models/models';
import { clearAuthToken, readAuthToken, storeAuthToken } from '../graphql.provider';

@Injectable({ providedIn: 'root' })
export class AuthService {
  /** Reactive current-user signal the navbar and route guard read from. */
  readonly currentUser = signal<AppUser | null>(this.loadCachedUser());

  constructor(private apollo: Apollo, private router: Router) {}

  get isAuthenticated(): boolean {
    return !!readAuthToken();
  }

  async login(email: string, password: string): Promise<void> {
    const result = await firstValueFrom(
      this.apollo.mutate<{ login: AuthPayload }>({
        mutation: LOGIN,
        variables: { input: { email, password } }
      })
    );

    const payload = result.data?.login;
    if (!payload) {
      throw new Error('Login failed.');
    }

    storeAuthToken(payload.token);
    localStorage.setItem('nexbridge_user', JSON.stringify(payload.user));
    this.currentUser.set(payload.user);
  }

  logout(): void {
    clearAuthToken();
    localStorage.removeItem('nexbridge_user');
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  private loadCachedUser(): AppUser | null {
    const raw = localStorage.getItem('nexbridge_user');
    return raw ? (JSON.parse(raw) as AppUser) : null;
  }
}
