import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AdminAccount, CommunityNeed, CreateContribution, CreateDonation, CreateNeed, DashboardSummary, Donation, NeedContribution, ReviewStatus, StatusHistoryItem } from '../models/donation.model';

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

  needs(): Observable<CommunityNeed[]> {
    return this.http.get<CommunityNeed[]>(`${this.apiUrl}/needs`);
  }

  createNeed(need: CreateNeed): Observable<{ id: string; reviewStatus: ReviewStatus }> {
    return this.http.post<{ id: string; reviewStatus: ReviewStatus }>(`${this.apiUrl}/needs`, need);
  }

  updateNeed(id: string, need: CreateNeed): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/needs/${id}`, need);
  }

  deleteNeed(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/needs/${id}`);
  }

  contributions(needId: string): Observable<NeedContribution[]> {
    return this.http.get<NeedContribution[]>(`${this.apiUrl}/needs/${needId}/contributions`);
  }

  contribute(needId: string, contribution: CreateContribution): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(`${this.apiUrl}/needs/${needId}/contributions`, contribution);
  }

  receiveContribution(id: string): Observable<{ status: string; successful: boolean }> {
    return this.http.post<{ status: string; successful: boolean }>(`${this.apiUrl}/contributions/${id}/received`, {});
  }

  adminAccounts(): Observable<AdminAccount[]> {
    return this.http.get<AdminAccount[]>(`${this.apiUrl}/admin/accounts`);
  }

  reviewAccount(id: string, status: 'Approved' | 'Rejected'): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/admin/accounts/${id}/review`, { status });
  }

  verificationDocumentUrl(id: string): string {
    return `${this.apiUrl}/admin/accounts/${id}/verification-document`;
  }

}
