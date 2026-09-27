import { Injectable, signal } from '@angular/core';
import { Apollo } from 'apollo-angular';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LOGIN, REGISTER } from '../graphql.operations';
import { AppUser, AuthPayload, Company } from '../models/models';
import { clearAuthToken, readAuthToken, storeAuthToken } from '../graphql.provider';

@Injectable({ providedIn: 'root' })
export class AuthService {
  /** Reactive current-user signal the navbar and route guard read from. */
  readonly currentUser = signal<AppUser | null>(this.loadCachedUser());
  readonly currentCompany = signal<Company | null>(this.loadCachedCompany());

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

    this.applyAuthPayload(payload);
  }

  async register(
    fullName: string, email: string, password: string,
    companyName: string, vatNumber: string, iban: string
  ): Promise<void> {
    const result = await firstValueFrom(
      this.apollo.mutate<{ register: AuthPayload }>({
        mutation: REGISTER,
        variables: { input: { fullName, email, password, companyName, vatNumber, iban } }
      })
    );

    const payload = result.data?.register;
    if (!payload) {
      throw new Error('Registration failed.');
    }

    this.applyAuthPayload(payload);
  }

  private applyAuthPayload(payload: AuthPayload): void {
    storeAuthToken(payload.token);
    localStorage.setItem('nexbridge_user', JSON.stringify(payload.user));
    localStorage.setItem('nexbridge_company', JSON.stringify(payload.company));
    this.currentUser.set(payload.user);
    this.currentCompany.set(payload.company);
  }

  logout(): void {
    clearAuthToken();
    localStorage.removeItem('nexbridge_user');
    localStorage.removeItem('nexbridge_company');
    this.currentUser.set(null);
    this.currentCompany.set(null);
    this.router.navigate(['/login']);
  }

  private loadCachedUser(): AppUser | null {
    const raw = localStorage.getItem('nexbridge_user');
    return raw ? (JSON.parse(raw) as AppUser) : null;
  }

  private loadCachedCompany(): Company | null {
    const raw = localStorage.getItem('nexbridge_company');
    return raw ? (JSON.parse(raw) as Company) : null;
  }
}
