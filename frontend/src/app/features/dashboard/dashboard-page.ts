import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import { Account } from '../../core/models/account.model';
import { CreateDonation, Donation, DonationStatus, DonationType, DashboardSummary, PendingRecipient, StatusHistoryItem } from '../../core/models/donation.model';
import { AuthService } from '../../core/services/auth.service';
import { DonationsService } from '../../core/services/donations.service';

@Component({
  selector: 'app-dashboard-page',
  imports: [FormsModule],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
})
export class DashboardPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly api = inject(DonationsService);
  private readonly router = inject(Router);
  readonly account = signal<Account | null>(null);
  readonly donations = signal<Donation[]>([]);
  readonly summary = signal<DashboardSummary>({ total: 0, active: 0, delivered: 0 });
  readonly pendingRecipients = signal<PendingRecipient[]>([]);
  readonly history = signal<Record<string, StatusHistoryItem[]>>({});
  readonly busy = signal(false);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly success = signal('');
  readonly selectedType = signal<'All' | DonationType>('All');
  readonly editingDonationId = signal<string | null>(null);
  readonly visibleDonations = computed(() => this.selectedType() === 'All' ? this.donations() : this.donations().filter((item) => item.type === this.selectedType()));
  title = '';
  description = '';
  type: DonationType = 'PreparedFood';
  quantity = '';
  unit = 'servings';
  pickupAddress = '';
  pickupFrom = '';
  pickupUntil = '';
  storageInstructions = '';
  allergens = '';

  ngOnInit(): void {
    this.auth.currentUser().subscribe({
      next: (account) => {
        this.account.set(account);
        this.loadDashboard();
      },
      error: () => void this.router.navigateByUrl('/login'),
    });
  }

  get isDonor(): boolean { return this.account()?.role === 'Donor'; }
  get isRecipient(): boolean { return this.account()?.role === 'Recipient'; }
  get isAdmin(): boolean { return this.account()?.role === 'Admin'; }

  createDonation(): void {
    if (this.busy()) return;
    const request = this.buildDonationRequest();
    if (!request) return;
    this.startAction();
    this.api.create(request).subscribe({
      next: () => {
        this.success.set('Your donation is live and visible to verified recipient organizations.');
        this.resetDonationForm();
        this.refresh();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  editDonation(donation: Donation): void {
    this.error.set('');
    this.success.set('');
    this.editingDonationId.set(donation.id);
    this.title = donation.title;
    this.description = donation.description;
    this.type = donation.type;
    this.quantity = donation.quantity;
    this.unit = donation.unit ?? '';
    this.pickupAddress = donation.pickupAddress;
    this.pickupFrom = this.toDateTimeInput(donation.pickupFrom);
    this.pickupUntil = this.toDateTimeInput(donation.pickupUntil);
    this.storageInstructions = donation.storageInstructions ?? '';
    this.allergens = donation.allergens ?? '';
    document.querySelector('.donation-form-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  saveDonation(): void {
    const id = this.editingDonationId();
    if (!id || this.busy()) return;
    const request = this.buildDonationRequest();
    if (!request) return;
    this.startAction();
    this.api.update(id, request).subscribe({
      next: () => {
        this.success.set('Your donation details have been updated.');
        this.resetDonationForm();
        this.refresh();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  deleteDonation(donation: Donation): void {
    if (this.busy() || !window.confirm(`Permanently delete “${donation.title}”?`)) return;
    this.startAction();
    this.api.delete(donation.id).subscribe({
      next: () => {
        this.success.set('The available donation has been deleted.');
        if (this.editingDonationId() === donation.id) this.resetDonationForm();
        this.refresh();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  cancelEdit(): void {
    this.resetDonationForm();
  }

  requestDonation(donation: Donation): void {
    this.runDonationAction(donation.id, this.api.request(donation.id), 'Request sent to the donor.');
  }

  acceptDonation(donation: Donation): void {
    this.runDonationAction(donation.id, this.api.accept(donation.id), 'Request accepted. Coordinate the pickup with the recipient.');
  }

  updateStatus(donation: Donation, status: DonationStatus): void {
    this.runDonationAction(donation.id, this.api.updateStatus(donation.id, status), `Donation updated to ${this.label(status)}.`);
  }

  approveRecipient(recipient: PendingRecipient): void {
    if (this.busy()) return;
    this.startAction();
    this.api.approveRecipient(recipient.id).subscribe({
      next: () => {
        this.success.set(`${recipient.organizationName} is verified and can now request donations.`);
        this.busy.set(false);
        this.loadDashboard();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  toggleHistory(donation: Donation): void {
    if (this.history()[donation.id]) {
      this.history.update((items) => { const next = { ...items }; delete next[donation.id]; return next; });
      return;
    }
    this.api.history(donation.id).subscribe({
      next: (items) => this.history.update((current) => ({ ...current, [donation.id]: items })),
      error: (error: unknown) => this.fail(error),
    });
  }

  logout(): void {
    this.auth.logout().subscribe({ next: () => void this.router.navigateByUrl('/login'), error: (error: unknown) => this.fail(error) });
  }

  label(value: string): string { return value.replace(/([a-z])([A-Z])/g, '$1 $2'); }
  formatDate(value: string): string { return new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }); }

  private loadDashboard(): void {
    this.loading.set(true);
    const requests = forkJoin({ summary: this.api.dashboard(), donations: this.api.list() });
    if (this.isAdmin) this.api.pendingRecipients().subscribe({ next: (items) => this.pendingRecipients.set(items), error: (error: unknown) => this.fail(error) });
    requests.subscribe({
      next: ({ summary, donations }) => { this.summary.set(summary); this.donations.set(donations); this.loading.set(false); },
      error: (error: unknown) => { this.loading.set(false); this.fail(error); },
    });
  }

  private refresh(): void {
    this.busy.set(false);
    this.loadDashboard();
  }

  private runDonationAction(_id: string, request: Observable<unknown>, message: string): void {
    if (this.busy()) return;
    this.startAction();
    request.subscribe({ next: () => { this.success.set(message); this.refresh(); }, error: (error: unknown) => this.fail(error) });
  }

  private startAction(): void { this.error.set(''); this.success.set(''); this.busy.set(true); }

  private fail(error: unknown): void {
    this.busy.set(false);
    if (error instanceof HttpErrorResponse && error.status === 403) {
      this.error.set('This action is unavailable for your account or organization.');
      return;
    }
    const message = error instanceof HttpErrorResponse && typeof error.error === 'object' && error.error !== null && 'error' in error.error && typeof error.error.error === 'string'
      ? error.error.error : 'Could not reach Hunger. Check the API connection and try again.';
    this.error.set(message);
  }

  private buildDonationRequest(): CreateDonation | null {
    if (!this.title.trim() || !this.description.trim() || !this.quantity.trim() || !this.pickupAddress.trim() || !this.pickupFrom || !this.pickupUntil) return null;
    const pickupFrom = new Date(this.pickupFrom);
    const pickupUntil = new Date(this.pickupUntil);
    if (Number.isNaN(pickupFrom.getTime()) || Number.isNaN(pickupUntil.getTime()) || pickupFrom <= new Date() || pickupUntil <= pickupFrom) {
      this.error.set('Enter a valid future pickup window. The deadline must be after the start time.');
      return null;
    }
    return {
      title: this.title.trim(), description: this.description.trim(), type: this.type,
      quantity: this.quantity.trim(), unit: this.unit.trim(), pickupAddress: this.pickupAddress.trim(),
      pickupFrom: pickupFrom.toISOString(), pickupUntil: pickupUntil.toISOString(),
      storageInstructions: this.storageInstructions.trim(), allergens: this.allergens.trim(),
    };
  }

  private toDateTimeInput(value: string): string {
    const date = new Date(value);
    return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
  }

  private resetDonationForm(): void {
    this.editingDonationId.set(null);
    this.title = ''; this.description = ''; this.quantity = ''; this.pickupAddress = '';
    this.pickupFrom = ''; this.pickupUntil = ''; this.storageInstructions = ''; this.allergens = '';
    this.type = 'PreparedFood'; this.unit = 'servings';
  }
}
