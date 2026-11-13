import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AdminAccount, CommunityNeed, Donation } from '../../../../core/models/donation.model';
import { DonationsService } from '../../../../core/services/donations.service';
import { AdminPortal } from './admin-portal';

describe('AdminPortal', () => {
  it('keeps content lists read-only and allows review of organization identity only', async () => {
    const account: AdminAccount = {
      id: 'account-id',
      name: 'Community Kitchen',
      email: 'kitchen@example.org',
      organizationName: 'Community Kitchen',
      role: 'Donor',
      verificationStatus: 'Pending',
      createdAt: '2026-01-01T10:00:00Z',
      hasVerificationDocument: true,
    };
    const donation: Donation = {
      id: 'donation-id',
      createdBy: 'account-id',
      recipientId: null,
      title: 'Community meals',
      description: 'Freshly prepared meals',
      type: 'PreparedFood',
      quantity: '12',
      unit: 'meals',
      pickupAddress: 'Community Kitchen',
      pickupFrom: '2026-12-01T10:00:00Z',
      pickupUntil: '2026-12-01T12:00:00Z',
      storageInstructions: null,
      allergens: null,
      status: 'Available',
      createdAt: '2026-01-01T10:00:00Z',
      donorOrganization: 'Community Kitchen',
      recipientOrganization: '',
      expiresAt: '2026-12-01T18:00:00Z',
      reviewStatus: 'Approved',
    };
    const need: CommunityNeed = {
      id: 'need-id',
      recipientId: 'recipient-id',
      title: 'School kits',
      description: 'Stationery for students',
      type: 'Stationery',
      quantity: 20,
      unit: 'kits',
      neededBy: '2026-12-15T10:00:00Z',
      status: 'Open',
      reviewStatus: 'Approved',
      fulfilledQuantity: 0,
      recipientOrganization: 'Learning Center',
      createdAt: '2026-01-01T10:00:00Z',
    };
    const service = {
      adminAccounts: vi.fn().mockReturnValue(of([account])),
      list: vi.fn().mockReturnValue(of([donation])),
      needs: vi.fn().mockReturnValue(of([need])),
      reviewAccount: vi.fn().mockReturnValue(of(void 0)),
      verificationDocumentUrl: vi.fn().mockReturnValue('/api/admin/accounts/account-id/verification-document'),
    };

    await TestBed.configureTestingModule({
      imports: [AdminPortal],
      providers: [{ provide: DonationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdminPortal);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#admin-review')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#donations')).toBeNull();
    expect(fixture.nativeElement.querySelector('#needs')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Community Kitchen');
    const proofLink = fixture.nativeElement.querySelector('.proof-document-link') as HTMLAnchorElement | null;
    expect(proofLink?.getAttribute('href')).toBe('/api/admin/accounts/account-id/verification-document');
    expect(service.verificationDocumentUrl).toHaveBeenCalledWith(account.id);
    const overviewCards = fixture.nativeElement.querySelectorAll('.admin-overview-card strong') as NodeListOf<HTMLElement>;
    expect(Array.from(overviewCards).map((item) => item.textContent?.trim())).toEqual(['1']);

    (fixture.nativeElement.querySelector('#donations-tab') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#donations')?.textContent).toContain('Community meals');
    expect(fixture.nativeElement.querySelector('#admin-review')).toBeNull();
    const donationSearch = fixture.nativeElement.querySelector('#donations input[type="search"]') as HTMLInputElement;
    donationSearch.value = 'no matching donation';
    donationSearch.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#donations')?.textContent).toContain('No items match these filters.');

    (fixture.nativeElement.querySelector('#needs-tab') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#needs')?.textContent).toContain('School kits');
    expect(fixture.nativeElement.querySelector('#donations')).toBeNull();

    (fixture.nativeElement.querySelector('#accounts-tab') as HTMLButtonElement).click();
    fixture.detectChanges();
    const accountApproveButton = fixture.nativeElement.querySelector('#admin-review tbody .primary-button') as HTMLButtonElement | null;
    expect(accountApproveButton).not.toBeNull();
    accountApproveButton?.click();
    fixture.detectChanges();

    expect(service.reviewAccount).toHaveBeenCalledWith(account.id, 'Approved');
    expect(fixture.nativeElement.textContent).toContain('Organization account approved.');
  });
});
