import { Component, EventEmitter, Input, OnInit, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, Observable } from 'rxjs';
import { CommunityNeed, CreateContribution, CreateDonation, Donation, DonationStatus, DonationType, NeedContribution, StatusHistoryItem } from '../../../../core/models/donation.model';
import { HUNGER_COPY, labelFor } from '../../../../core/i18n/hunger-copy';
import { DonationsService } from '../../../../core/services/donations.service';
import { ExpandableRecord } from '../../shared/expandable-record';
import { FilterOption, PortalListFilters } from '../../shared/portal-list-filters';

@Component({
  selector: 'app-donor-portal',
  imports: [ExpandableRecord, FormsModule, PortalListFilters],
  templateUrl: './donor-portal.html',
  styleUrls: ['../../dashboard-page.css', '../../dashboard-workflows.css', './donor-portal.css'],
})
export class DonorPortal implements OnInit {
  private readonly api = inject(DonationsService);

  @Input({ required: true }) approved = false;
  @Input() activeTab = 'overview';
  @Output() tabChange = new EventEmitter<string>();

  readonly copy = HUNGER_COPY.donor;
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
  readonly needType = signal('All');
  readonly needStatus = signal('All');
  readonly typeOptions: FilterOption[] = [
    { value: 'All', label: this.commonCopy.all },
    { value: 'PreparedFood', label: this.commonCopy.preparedFood },
    { value: 'Groceries', label: this.commonCopy.groceries },
    { value: 'Stationery', label: this.commonCopy.stationery },
  ];
  readonly donationStatusOptions: FilterOption[] = this.optionsFor(['Available', 'Requested', 'Accepted', 'PickedUp', 'Delivered', 'Cancelled']);
  readonly needStatusOptions: FilterOption[] = this.optionsFor(['Open', 'PartiallyFulfilled', 'Fulfilled', 'Closed']);
  readonly editingDonationId = signal<string | null>(null);
  readonly selectedNeedId = signal<string | null>(null);
  readonly visibleDonations = computed(() => {
    const query = this.donationQuery().trim().toLocaleLowerCase();
    return this.donations().filter((donation) =>
      (this.selectedType() === 'All' || donation.type === this.selectedType()) &&
      (this.donationStatus() === 'All' || donation.status === this.donationStatus()) &&
      (!query || `${donation.title} ${donation.description} ${donation.recipientOrganization}`.toLocaleLowerCase().includes(query)));
  });
  readonly visibleNeeds = computed(() => {
    const query = this.needQuery().trim().toLocaleLowerCase();
    return this.needs().filter((need) =>
      (this.needType() === 'All' || need.type === this.needType()) &&
      (this.needStatus() === 'All' || need.status === this.needStatus()) &&
      (!query || `${need.title} ${need.description} ${need.recipientOrganization}`.toLocaleLowerCase().includes(query)));
  });
  readonly publishedDonationCount = computed(() => this.donations().filter((donation) => donation.reviewStatus === 'Approved').length);
  readonly donationRequestsCount = computed(() => this.donations().filter((donation) => donation.status === 'Requested').length);
  readonly openCommunityNeedsCount = computed(() => this.needs().filter((need) =>
    need.reviewStatus === 'Approved' && ['Open', 'PartiallyFulfilled'].includes(need.status)).length);

  title = '';
  description = '';
  type: DonationType = 'PreparedFood';
  quantity = '';
  unit = 'servings';
  pickupAddress = '';
  pickupFrom = '';
  pickupUntil = '';
  useBy = '';
  storageInstructions = '';
  allergens = '';
  contributionQuantity = '';
  contributionAddress = '';
  contributionFrom = '';
  contributionUntil = '';
  contributionUseBy = '';
  contributionStorage = '';
  contributionAllergens = '';

  ngOnInit(): void {
    this.load();
  }

  createDonation(): void {
    const request = this.buildDonationRequest();
    if (!request || this.busy()) return;

    this.startAction();
    this.api.create(request).subscribe({
      next: () => {
        this.success.set(this.copy.donationSaved);
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
    this.useBy = donation.expiresAt ? this.toDateTimeInput(donation.expiresAt) : '';
    this.storageInstructions = donation.storageInstructions ?? '';
    this.allergens = donation.allergens ?? '';
    this.tabChange.emit('create');
  }

  saveDonation(): void {
    const id = this.editingDonationId();
    const request = this.buildDonationRequest();
    if (!id || !request || this.busy()) return;

    this.startAction();
    this.api.update(id, request).subscribe({
      next: () => {
        this.success.set(this.copy.donationUpdated);
        this.resetDonationForm();
        this.refresh();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  cancelEdit(): void {
    this.resetDonationForm();
  }

  deleteDonation(donation: Donation): void {
    if (this.busy() || !window.confirm(`${this.copy.confirmDelete} “${donation.title}”?`)) return;

    this.runAction(this.api.delete(donation.id), this.copy.donationDeleted);
  }

  acceptDonation(donation: Donation): void {
    this.runAction(this.api.accept(donation.id), this.copy.requestAccepted);
  }

  cancelDonation(donation: Donation): void {
    this.updateDonationStatus(donation, 'Cancelled');
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

  startContribution(need: CommunityNeed): void {
    this.selectedNeedId.set(this.selectedNeedId() === need.id ? null : need.id);
    this.contributionQuantity = '';
    this.contributionAddress = '';
    this.contributionFrom = '';
    this.contributionUntil = '';
    this.contributionUseBy = '';
    this.contributionStorage = '';
    this.contributionAllergens = '';
  }

  submitContribution(need: CommunityNeed): void {
    const quantity = Number(this.contributionQuantity);
    const from = new Date(this.contributionFrom);
    const until = new Date(this.contributionUntil);
    const expiry = new Date(this.contributionUseBy);

    if (this.busy() || !Number.isFinite(quantity) || quantity <= 0 || !this.contributionAddress.trim() ||
      Number.isNaN(from.getTime()) || Number.isNaN(until.getTime()) || from <= new Date() || until <= from ||
      (need.type !== 'Stationery' && (Number.isNaN(expiry.getTime()) || expiry < until))) {
      this.error.set(this.copy.contributionValidation);
      return;
    }

    const contribution: CreateContribution = {
      quantity,
      unit: need.unit,
      pickupAddress: this.contributionAddress.trim(),
      pickupFrom: from.toISOString(),
      pickupUntil: until.toISOString(),
      expiresAt: need.type === 'Stationery' ? null : expiry.toISOString(),
      storageInstructions: this.contributionStorage.trim(),
      allergens: this.contributionAllergens.trim(),
    };

    this.startAction();
    this.api.contribute(need.id, contribution).subscribe({
      next: () => {
        this.success.set(this.copy.contributionSaved);
        this.selectedNeedId.set(null);
        this.refresh();
      },
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

  donationProgress(donation: Donation): number {
    switch (donation.status) {
      case 'Requested': return 25;
      case 'Accepted': return 50;
      case 'PickedUp': return 75;
      case 'Delivered': return 100;
      case 'Available':
      case 'Cancelled': return 0;
    }
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

  private updateDonationStatus(donation: Donation, status: DonationStatus): void {
    const message = status === 'Cancelled' ? this.copy.donationCancelled : this.copy.donationUpdated;
    this.runAction(this.api.updateStatus(donation.id, status), message);
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

  private fail(_error: unknown): void {
    this.busy.set(false);
    this.error.set(this.commonCopy.apiFailure);
  }

  private refresh(): void {
    this.busy.set(false);
    this.load();
  }

  private buildDonationRequest(): CreateDonation | null {
    if (!this.title.trim() || !this.description.trim() || !this.quantity.trim() ||
      !this.pickupAddress.trim() || !this.pickupFrom || !this.pickupUntil) {
      this.error.set(this.copy.donationValidation);
      return null;
    }

    const pickupFrom = new Date(this.pickupFrom);
    const pickupUntil = new Date(this.pickupUntil);
    const expiry = new Date(this.useBy);

    if (Number.isNaN(pickupFrom.getTime()) || Number.isNaN(pickupUntil.getTime()) ||
      pickupFrom <= new Date() || pickupUntil <= pickupFrom ||
      (this.type !== 'Stationery' && (Number.isNaN(expiry.getTime()) || expiry < pickupUntil))) {
      this.error.set(this.copy.dateValidation);
      return null;
    }

    return {
      title: this.title.trim(),
      description: this.description.trim(),
      type: this.type,
      quantity: this.quantity.trim(),
      unit: this.unit.trim(),
      pickupAddress: this.pickupAddress.trim(),
      pickupFrom: pickupFrom.toISOString(),
      pickupUntil: pickupUntil.toISOString(),
      storageInstructions: this.storageInstructions.trim(),
      allergens: this.allergens.trim(),
      expiresAt: this.type === 'Stationery' ? null : expiry.toISOString(),
    };
  }

  private toDateTimeInput(value: string): string {
    const date = new Date(value);
    return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
  }

  private resetDonationForm(): void {
    this.editingDonationId.set(null);
    this.title = '';
    this.description = '';
    this.quantity = '';
    this.pickupAddress = '';
    this.pickupFrom = '';
    this.pickupUntil = '';
    this.useBy = '';
    this.storageInstructions = '';
    this.allergens = '';
    this.type = 'PreparedFood';
    this.unit = 'servings';
  }
}
