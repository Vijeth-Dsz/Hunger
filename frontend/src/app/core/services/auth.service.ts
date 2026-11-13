import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Account, PortalRole, RegisterRequest } from '../models/account.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/auth';

  roles(): Observable<PortalRole[]> {
    return this.http.get<PortalRole[]>('/api/roles');
  }

  register(account: RegisterRequest): Observable<Account> {
    const formData = new FormData();
    formData.append('name', account.name);
    formData.append('email', account.email);
    formData.append('organizationName', account.organizationName);
    formData.append('role', account.role);
    formData.append('password', account.password);
    formData.append('verificationDocument', account.verificationDocument, account.verificationDocument.name);
    return this.http.post<Account>(`${this.apiUrl}/register`, formData);
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
