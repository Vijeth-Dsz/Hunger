import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { DonationsService } from '../../../../core/services/donations.service';
import { RecipientPortal } from './recipient-portal';

describe('RecipientPortal tabs', () => {
  it('separates the request form, request list, and available donation list', async () => {
    const service = {
      list: vi.fn().mockReturnValue(of([])),
      needs: vi.fn().mockReturnValue(of([])),
      contributions: vi.fn().mockReturnValue(of([])),
    };

    await TestBed.configureTestingModule({
      imports: [RecipientPortal],
      providers: [{ provide: DonationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(RecipientPortal);
    fixture.componentRef.setInput('approved', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#recipient-overview')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#request-support')).toBeNull();
    expect(fixture.nativeElement.querySelector('#needs')).toBeNull();
    expect(fixture.nativeElement.querySelector('#donations')).toBeNull();

    fixture.componentRef.setInput('activeTab', 'create');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#request-support')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#needs')).toBeNull();
    expect(fixture.nativeElement.querySelector('#donations')).toBeNull();

    fixture.componentRef.setInput('activeTab', 'requests');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#needs')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#request-support')).toBeNull();

    fixture.componentRef.setInput('activeTab', 'donations');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#donations')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#needs')).toBeNull();
  });

  it('shows current review, request, and contribution statuses and can refresh them', async () => {
    const need = {
      id: 'need-1', recipientId: 'recipient-1', title: 'Rice for families', description: 'Rice needed for local families',
      type: 'Groceries' as const, quantity: 20, unit: 'kg', neededBy: '2027-01-01T10:00:00Z', status: 'PartiallyFulfilled' as const,
      reviewStatus: 'Approved' as const, fulfilledQuantity: 5, recipientOrganization: 'Community Centre', createdAt: '2026-11-01T10:00:00Z',
    };
    const service = {
      list: vi.fn().mockReturnValue(of([])),
      needs: vi.fn().mockReturnValue(of([need])),
      contributions: vi.fn().mockReturnValue(of([{
        id: 'contribution-1', needId: need.id, donorId: 'donor-1', quantity: 5, unit: 'kg', pickupAddress: 'Community Centre',
        pickupFrom: '2026-11-20T10:00:00Z', pickupUntil: '2026-11-20T12:00:00Z', expiresAt: null,
        storageInstructions: null, allergens: null, status: 'Delivered' as const, donorOrganization: 'Local Market',
        recipientOrganization: need.recipientOrganization, needTitle: need.title, createdAt: '2026-11-14T10:00:00Z',
      }])),
    };

    await TestBed.configureTestingModule({
      imports: [RecipientPortal],
      providers: [{ provide: DonationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(RecipientPortal);
    fixture.componentRef.setInput('approved', true);
    fixture.componentRef.setInput('activeTab', 'requests');
    fixture.detectChanges();

    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('Rice needed for local families');
    expect(content).toContain('Approved');
    expect(content).toContain('Partially fulfilled');
    expect(content).toContain('Successful');
    expect(content).toContain('5 / 20 kg received');

    const search = fixture.nativeElement.querySelector('#needs input[type="search"]') as HTMLInputElement;
    search.value = 'unlisted supply';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#needs')?.textContent).toContain('No items match these filters.');

    search.value = '';
    search.dispatchEvent(new Event('input'));
    const category = fixture.nativeElement.querySelector('#needs select') as HTMLSelectElement;
    category.value = 'Stationery';
    category.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#needs')?.textContent).toContain('No items match these filters.');

    category.value = 'Groceries';
    category.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('#needs .section-heading button') as HTMLButtonElement).click();
    expect(service.needs).toHaveBeenCalledTimes(2);
  });
});
