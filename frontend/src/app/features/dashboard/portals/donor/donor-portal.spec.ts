import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { DonationsService } from '../../../../core/services/donations.service';
import { DonorPortal } from './donor-portal';

describe('DonorPortal tabs', () => {
  it('shows only the selected donor workspace view and emits overview navigation', async () => {
    const service = {
      list: vi.fn().mockReturnValue(of([])),
      needs: vi.fn().mockReturnValue(of([])),
      contributions: vi.fn().mockReturnValue(of([])),
    };

    await TestBed.configureTestingModule({
      imports: [DonorPortal],
      providers: [{ provide: DonationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(DonorPortal);
    fixture.componentRef.setInput('approved', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#donor-overview')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#donation-form')).toBeNull();
    expect(fixture.nativeElement.querySelector('#donations')).toBeNull();
    expect(fixture.nativeElement.querySelector('#needs')).toBeNull();

    const navigation = vi.fn();
    fixture.componentInstance.tabChange.subscribe(navigation);
    const createLink = fixture.nativeElement.querySelector('.guide-link') as HTMLButtonElement;
    createLink.click();
    expect(navigation).toHaveBeenCalledWith('create');

    fixture.componentRef.setInput('activeTab', 'create');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#donation-form')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('input[name="useBy"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#donor-overview')).toBeNull();
    expect(fixture.nativeElement.querySelector('#donations')).toBeNull();

    fixture.componentRef.setInput('activeTab', 'donations');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#donations')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#donation-form')).toBeNull();
  });

  it('shows recipient request details, review status, fulfillment status, and contribution status', async () => {
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
        pickupFrom: '2026-11-20T10:00:00Z', pickupUntil: '2026-11-20T12:00:00Z',
        expiresAt: null, storageInstructions: null, allergens: null, status: 'Approved' as const, donorOrganization: 'Local Market',
        recipientOrganization: need.recipientOrganization, needTitle: need.title, createdAt: '2026-11-14T10:00:00Z',
      }])),
    };

    await TestBed.configureTestingModule({
      imports: [DonorPortal],
      providers: [{ provide: DonationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(DonorPortal);
    fixture.componentRef.setInput('approved', true);
    fixture.componentRef.setInput('activeTab', 'community');
    fixture.detectChanges();

    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('Rice needed for local families');
    expect(content).toContain('Approved');
    expect(content).toContain('Partially fulfilled');
    expect(content).toContain('Approved');

    const search = fixture.nativeElement.querySelector('#needs input[type="search"]') as HTMLInputElement;
    search.value = 'no matching request';
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
