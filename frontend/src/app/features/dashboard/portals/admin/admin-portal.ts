import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { AdminAccount, CommunityNeed, Donation } from '../../../../core/models/donation.model';
import { HUNGER_COPY, labelFor } from '../../../../core/i18n/hunger-copy';
import { DonationsService } from '../../../../core/services/donations.service';
import { FilterOption, PortalListFilters } from '../../shared/portal-list-filters';

@Component({
  selector: 'app-admin-portal',
  imports: [PortalListFilters],
  templateUrl: './admin-portal.html',
  styleUrls: ['../../dashboard-page.css', '../../dashboard-workflows.css', './admin-portal.css'],
})
export class AdminPortal implements OnInit {
  readonly api = inject(DonationsService);

  readonly copy = HUNGER_COPY.admin;
  readonly commonCopy = HUNGER_COPY.common;
  readonly dashboardCopy = HUNGER_COPY.dashboard;
  readonly accounts = signal<AdminAccount[]>([]);
  readonly donations = signal<Donation[]>([]);
  readonly needs = signal<CommunityNeed[]>([]);
  readonly busy = signal(false);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly success = signal('');
  readonly activeTab = signal<'accounts' | 'donations' | 'needs'>('accounts');
  readonly accountQuery = signal('');
  readonly accountRole = signal('All');
  readonly accountStatus = signal('All');
  readonly donationQuery = signal('');
  readonly donationType = signal('All');
  readonly donationStatus = signal('All');
  readonly needQuery = signal('');
  readonly needType = signal('All');
  readonly needStatus = signal('All');
  readonly allTypes: FilterOption[] = [
    { value: 'All', label: this.commonCopy.all },
    { value: 'PreparedFood', label: this.commonCopy.preparedFood },
    { value: 'Groceries', label: this.commonCopy.groceries },
    { value: 'Stationery', label: this.commonCopy.stationery },
  ];
  readonly accountRoles: FilterOption[] = [
    { value: 'All', label: this.commonCopy.all },
    { value: 'Donor', label: labelFor('Donor') },
    { value: 'Recipient', label: labelFor('Recipient') },
  ];
  readonly accountStatuses = this.optionsFor(['Pending', 'Approved', 'Rejected']);
  readonly donationStatuses = this.optionsFor(['Available', 'Requested', 'Accepted', 'PickedUp', 'Delivered', 'Cancelled']);
  readonly needStatuses = this.optionsFor(['Open', 'PartiallyFulfilled', 'Fulfilled', 'Closed']);
  readonly pendingAccounts = computed(() => this.accounts().filter((account) => account.verificationStatus === 'Pending'));
  readonly filteredAccounts = computed(() => {
    const query = this.accountQuery().trim().toLocaleLowerCase();
    return this.accounts().filter((account) =>
      (this.accountRole() === 'All' || account.role === this.accountRole()) &&
      (this.accountStatus() === 'All' || account.verificationStatus === this.accountStatus()) &&
      (!query || `${account.name} ${account.email} ${account.organizationName}`.toLocaleLowerCase().includes(query)));
  });
  readonly filteredDonations = computed(() => {
    const query = this.donationQuery().trim().toLocaleLowerCase();
    return this.donations().filter((donation) =>
      (this.donationType() === 'All' || donation.type === this.donationType()) &&
      (this.donationStatus() === 'All' || donation.status === this.donationStatus()) &&
      (!query || `${donation.title} ${donation.description} ${donation.donorOrganization}`.toLocaleLowerCase().includes(query)));
  });
  readonly filteredNeeds = computed(() => {
    const query = this.needQuery().trim().toLocaleLowerCase();
    return this.needs().filter((need) =>
      (this.needType() === 'All' || need.type === this.needType()) &&
      (this.needStatus() === 'All' || need.status === this.needStatus()) &&
      (!query || `${need.title} ${need.description} ${need.recipientOrganization}`.toLocaleLowerCase().includes(query)));
  });

  ngOnInit(): void {
    this.load();
  }

  reviewAccount(account: AdminAccount, status: 'Approved' | 'Rejected'): void {
    const message = status === 'Approved' ? this.copy.accountApproved : this.copy.accountRejected;
    this.runAction(this.api.reviewAccount(account.id, status), message);
  }

  label(value: string): string {
    return labelFor(value);
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
  }

  private optionsFor(values: string[]): FilterOption[] {
    return [
      { value: 'All', label: this.commonCopy.all },
      ...values.map((value) => ({ value, label: labelFor(value) })),
    ];
  }

  private load(): void {
    this.loading.set(true);
    forkJoin({
      accounts: this.api.adminAccounts(),
      donations: this.api.list(),
      needs: this.api.needs(),
    }).subscribe({
      next: ({ accounts, donations, needs }) => {
        this.accounts.set(accounts);
        this.donations.set(donations);
        this.needs.set(needs);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.fail(error);
      },
    });
  }

  private runAction(request: Observable<unknown>, message: string): void {
    if (this.busy()) return;
    this.error.set('');
    this.success.set('');
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.success.set(message);
        this.busy.set(false);
        this.load();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  private fail(_error: unknown): void {
    this.busy.set(false);
    this.error.set(this.commonCopy.apiFailure);
  }
}
