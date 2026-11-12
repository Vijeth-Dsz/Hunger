import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Account, RegisterRequest } from '../models/account.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/auth';

  register(account: RegisterRequest): Observable<Account> {
    return this.http.post<Account>(`${this.apiUrl}/register`, account);
  }

  login(email: string, password: string): Observable<Account> {
    return this.http.post<Account>(`${this.apiUrl}/login`, { email, password });
  }

  currentUser(): Observable<Account> {
    return this.http.get<Account>(`${this.apiUrl}/me`);
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/logout`, {});
  }
}
