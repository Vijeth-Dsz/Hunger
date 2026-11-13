import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnInit, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, Observable } from 'rxjs';
import { CommunityNeed, CreateNeed, Donation, DonationStatus, DonationType, NeedContribution, StatusHistoryItem } from '../../../../core/models/donation.model';
import { HUNGER_COPY, labelFor } from '../../../../core/i18n/hunger-copy';
import { DonationsService } from '../../../../core/services/donations.service';
import { ExpandableRecord } from '../../shared/expandable-record';
import { FilterOption, PortalListFilters } from '../../shared/portal-list-filters';

@Component({
  selector: 'app-recipient-portal',
  imports: [ExpandableRecord, FormsModule, PortalListFilters],
  templateUrl: './recipient-portal.html',
  styleUrls: ['../../dashboard-page.css', '../../dashboard-workflows.css', './recipient-portal.css'],
})
export class RecipientPortal implements OnInit {
  private readonly api = inject(DonationsService);

  @Input({ required: true }) approved = false;
  @Input() activeTab = 'overview';
  @Output() tabChange = new EventEmitter<string>();

  readonly copy = HUNGER_COPY.recipient;
  readonly commonCopy = HUNGER_COPY.common;
  readonly dashboardCopy = HUNGER_COPY.dashboard;
  readonly donations = signal<Donation[]>([]);
  readonly needs = signal<CommunityNeed[]>([]);
  readonly contributions = signal<Record<string, NeedContribution[]>>({});
  readonly history = signal<Record<string, StatusHistoryItem[]>>({});
  readonly busy = signal(false);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly success = signal('');
  readonly selectedType = signal<string>('All');
  readonly donationQuery = signal('');
  readonly donationStatus = signal('All');
  readonly needQuery = signal('');
  readonly filterNeedType = signal('All');
  readonly needStatus = signal('All');
  readonly typeOptions: FilterOption[] = [
    { value: 'All', label: this.commonCopy.all },
    { value: 'PreparedFood', label: this.commonCopy.preparedFood },
    { value: 'Groceries', label: this.commonCopy.groceries },
    { value: 'Stationery', label: this.commonCopy.stationery },
  ];
  readonly donationStatusOptions: FilterOption[] = this.optionsFor(['Available', 'Requested', 'Accepted', 'PickedUp', 'Delivered', 'Cancelled']);
  readonly needStatusOptions: FilterOption[] = this.optionsFor(['Open', 'PartiallyFulfilled', 'Fulfilled', 'Closed']);
  readonly editingNeedId = signal<string | null>(null);
  readonly visibleDonations = computed(() => {
    const query = this.donationQuery().trim().toLocaleLowerCase();
    return this.donations().filter((donation) =>
      (this.selectedType() === 'All' || donation.type === this.selectedType()) &&
      (this.donationStatus() === 'All' || donation.status === this.donationStatus()) &&
      (!query || `${donation.title} ${donation.description} ${donation.donorOrganization}`.toLocaleLowerCase().includes(query)));
  });
  readonly visibleNeeds = computed(() => {
    const query = this.needQuery().trim().toLocaleLowerCase();
    return this.needs().filter((need) =>
      (this.filterNeedType() === 'All' || need.type === this.filterNeedType()) &&
      (this.needStatus() === 'All' || need.status === this.needStatus()) &&
      (!query || `${need.title} ${need.description}`.toLocaleLowerCase().includes(query)));
  });
  readonly publishedRequestCount = computed(() => this.needs().filter((need) => need.reviewStatus === 'Approved').length);
  readonly openRequestsCount = computed(() => this.needs().filter((need) =>
    need.reviewStatus === 'Approved' && ['Open', 'PartiallyFulfilled'].includes(need.status)).length);
  readonly availableDonationsCount = computed(() => this.donations().filter((donation) =>
    donation.status === 'Available' && donation.reviewStatus === 'Approved').length);
  readonly activeDonationsCount = computed(() => this.donations().filter((donation) =>
    ['Requested', 'Accepted', 'PickedUp'].includes(donation.status)).length);

  needTitle = '';
  needDescription = '';
  needType: DonationType = 'PreparedFood';
  needQuantity = '';
  needUnit = '';
  neededBy = '';

  ngOnInit(): void {
    this.load();
  }

  saveNeed(): void {
    if (this.busy()) return;
    const request = this.buildNeedRequest();
    if (!request) return;

    const needId = this.editingNeedId();
    this.startAction();
    const action: Observable<unknown> = needId
      ? this.api.updateNeed(needId, request)
      : this.api.createNeed(request);
    action.subscribe({
      next: () => {
        this.success.set(needId ? this.copy.requestUpdated : this.copy.requestSaved);
        this.resetNeedForm();
        this.refresh();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  editNeed(need: CommunityNeed): void {
    if (!this.canEditNeed(need)) {
      this.error.set(this.copy.editUnavailable);
      return;
    }

    this.error.set('');
    this.success.set('');
    this.editingNeedId.set(need.id);
    this.needTitle = need.title;
    this.needDescription = need.description;
    this.needType = need.type;
    this.needQuantity = String(need.quantity);
    this.needUnit = need.unit;
    this.neededBy = this.toDateTimeInput(need.neededBy);
    this.tabChange.emit('create');
  }

  cancelEdit(): void {
    this.resetNeedForm();
    this.error.set('');
    this.success.set('');
  }

  canEditNeed(need: CommunityNeed): boolean {
    const contributions = this.contributions()[need.id];
    return need.status === 'Open' && contributions !== undefined && contributions.length === 0;
  }

  deleteNeed(need: CommunityNeed): void {
    if (this.busy() || !this.canEditNeed(need) ||
      !window.confirm(`${this.copy.deleteConfirmation} “${need.title}”?`)) return;

    this.runAction(this.api.deleteNeed(need.id), this.copy.requestDeleted);
  }

  requestDonation(donation: Donation): void {
    this.runAction(this.api.request(donation.id), this.copy.requestSent);
  }

  updateDonation(donation: Donation, status: DonationStatus): void {
    const message = status === 'Delivered' ? this.copy.receivedSuccess : this.copy.pickedUpSuccess;
    this.runAction(this.api.updateStatus(donation.id, status), message);
  }

  receiveContribution(contribution: NeedContribution): void {
    this.runAction(this.api.receiveContribution(contribution.id), this.copy.receivedSuccess);
  }

  toggleHistory(donation: Donation): void {
    if (this.history()[donation.id]) {
      this.history.update((items) => {
        const next = { ...items };
        delete next[donation.id];
        return next;
      });
      return;
    }

    this.api.history(donation.id).subscribe({
      next: (items) => this.history.update((current) => ({ ...current, [donation.id]: items })),
      error: (error: unknown) => this.fail(error),
    });
  }

  label(value: string): string {
    return labelFor(value);
  }

  statusLabel(value: DonationStatus | NeedContribution['status'] | CommunityNeed['status']): string {
    return labelFor(value);
  }

  refreshStatuses(): void {
    this.load();
  }

  progress(need: CommunityNeed): number {
    return Math.min(100, need.fulfilledQuantity / need.quantity * 100);
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
    forkJoin({ donations: this.api.list(), needs: this.api.needs() }).subscribe({
      next: ({ donations, needs }) => {
        this.donations.set(donations);
        this.needs.set(needs);
        this.loading.set(false);
        needs.forEach((need) => this.loadContributions(need));
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.fail(error);
      },
    });
  }

  private loadContributions(need: CommunityNeed): void {
    this.api.contributions(need.id).subscribe({
      next: (items) => this.contributions.update((current) => ({ ...current, [need.id]: items })),
      error: (error: unknown) => this.fail(error),
    });
  }

  private runAction(request: Observable<unknown>, message: string): void {
    if (this.busy()) return;
    this.startAction();
    request.subscribe({
      next: () => {
        this.success.set(message);
        this.refresh();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  private startAction(): void {
    this.error.set('');
    this.success.set('');
    this.busy.set(true);
  }

  private fail(error: unknown): void {
    this.busy.set(false);
    this.error.set(error instanceof HttpErrorResponse && error.status === 409
      ? this.commonCopy.conflict
      : this.commonCopy.apiFailure);
  }

  private refresh(): void {
    this.busy.set(false);
    this.load();
  }

  private buildNeedRequest(): CreateNeed | null {
    const neededByDate = new Date(this.neededBy);
    const quantity = Number(this.needQuantity);
    if (!this.needTitle.trim() || !this.needDescription.trim() || !this.needUnit.trim() ||
      !Number.isFinite(quantity) || quantity <= 0 || !this.neededBy || neededByDate <= new Date()) {
      this.error.set(this.copy.requestValidation);
      return null;
    }

    return {
      title: this.needTitle.trim(),
      description: this.needDescription.trim(),
      type: this.needType,
      quantity,
      unit: this.needUnit.trim(),
      neededBy: neededByDate.toISOString(),
    };
  }

  private toDateTimeInput(value: string): string {
    const date = new Date(value);
    return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
  }

  private resetNeedForm(): void {
    this.editingNeedId.set(null);
    this.needTitle = '';
    this.needDescription = '';
    this.needType = 'PreparedFood';
    this.needQuantity = '';
    this.needUnit = '';
    this.neededBy = '';
  }
}
