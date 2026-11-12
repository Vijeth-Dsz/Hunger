import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateDonation, DashboardSummary, Donation, PendingRecipient, StatusHistoryItem } from '../models/donation.model';

@Injectable({ providedIn: 'root' })
export class DonationsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api';

  dashboard(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.apiUrl}/dashboard`);
  }

  list(): Observable<Donation[]> {
    return this.http.get<Donation[]>(`${this.apiUrl}/donations`);
  }

  create(donation: CreateDonation): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(`${this.apiUrl}/donations`, donation);
  }

  update(id: string, donation: CreateDonation): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/donations/${id}`, donation);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/donations/${id}`);
  }

  request(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/donations/${id}/request`, {});
  }

  accept(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/donations/${id}/accept`, {});
  }

  updateStatus(id: string, status: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/donations/${id}/status`, { status });
  }

  history(id: string): Observable<StatusHistoryItem[]> {
    return this.http.get<StatusHistoryItem[]>(`${this.apiUrl}/donations/${id}/history`);
  }

  pendingRecipients(): Observable<PendingRecipient[]> {
    return this.http.get<PendingRecipient[]>(`${this.apiUrl}/admin/recipients`);
  }

  approveRecipient(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/admin/recipients/${id}/approve`, {});
  }
}
