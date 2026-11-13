import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Account } from '../../core/models/account.model';
import { DashboardSummary } from '../../core/models/donation.model';
import { HUNGER_COPY, labelFor } from '../../core/i18n/hunger-copy';
import { AuthService } from '../../core/services/auth.service';
import { DonationsService } from '../../core/services/donations.service';
import { AdminPortal } from './portals/admin/admin-portal';
import { DonorPortal } from './portals/donor/donor-portal';
import { RecipientPortal } from './portals/recipient/recipient-portal';
import { PortalTab, PortalTabs } from './shared/portal-tabs';

@Component({
  selector: 'app-dashboard-shell',
  imports: [AdminPortal, DonorPortal, PortalTabs, RecipientPortal],
  templateUrl: './dashboard-shell.html',
  styleUrls: ['./dashboard-page.css', './dashboard-workflows.css', './dashboard-shell.css'],
})
export class DashboardShell implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly api = inject(DonationsService);
  private readonly router = inject(Router);

  readonly copy = HUNGER_COPY.dashboard;
  readonly commonCopy = HUNGER_COPY.common;
  readonly account = signal<Account | null>(null);
  readonly summary = signal<DashboardSummary>({ total: 0, active: 0, accepted: 0, delivered: 0 });
  readonly loading = signal(true);
  readonly error = signal('');
  readonly activePortalTab = signal('overview');

  get roleImage(): string {
    if (this.isDonor) return '/images/donor-portal.svg';
    if (this.isRecipient) return '/images/recipient-portal.svg';
    return '/images/admin-portal.svg';
  }

  get roleImageAlt(): string {
    if (this.isDonor) return this.copy.donorIllustrationAlt;
    if (this.isRecipient) return this.copy.recipientIllustrationAlt;
    return this.copy.adminIllustrationAlt;
  }

  get roleIconPath(): string {
    if (this.isDonor) return 'M12 21s-8-4.5-8-10a4.5 4.5 0 0 1 8-2.8A4.5 4.5 0 0 1 20 11c0 5.5-8 10-8 10Z';
    if (this.isRecipient) return 'M3 10 12 3l9 7v10H3V10Zm5 10v-6h8v6';
    return 'M12 3 20 6v5c0 5-3.4 8.3-8 10-4.6-1.7-8-5-8-10V6l8-3Z';
  }

  get portalTabs(): PortalTab[] {
    return this.isDonor
      ? [
          { id: 'overview', label: this.commonCopy.overview, icon: 'overview' },
          { id: 'create', label: this.copy.createDonationTab, icon: 'create' },
          { id: 'donations', label: this.copy.donorListings, icon: 'donations' },
          { id: 'community', label: this.copy.communityRequests, icon: 'community' },
        ]
      : [
          { id: 'overview', label: this.commonCopy.overview, icon: 'overview' },
          { id: 'create', label: this.copy.requestSupplies, icon: 'create' },
          { id: 'requests', label: this.copy.myRequests, icon: 'requests' },
          { id: 'donations', label: this.copy.availableDonations, icon: 'donations' },
        ];
  }

  ngOnInit(): void {
    this.auth.currentUser().subscribe({
      next: (account) => {
        this.account.set(account);
        this.loadSummary();
      },
      error: () => void this.router.navigateByUrl('/login'),
    });
  }

  get isDonor(): boolean {
    return this.account()?.role === 'Donor';
  }

  get isRecipient(): boolean {
    return this.account()?.role === 'Recipient';
  }

  get isAdmin(): boolean {
    return this.account()?.role === 'Admin';
  }

  label(value: string): string {
    return labelFor(value);
  }

  selectPortalTab(tab: string): void {
    this.activePortalTab.set(tab);
  }

  logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: (error: unknown) => this.fail(error),
    });
  }

  private loadSummary(): void {
    this.loading.set(true);
    this.api.dashboard().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.fail(error);
      },
    });
  }

  private fail(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      void this.router.navigateByUrl('/login');
      return;
    }

    this.error.set(this.commonCopy.apiFailure);
  }
}
