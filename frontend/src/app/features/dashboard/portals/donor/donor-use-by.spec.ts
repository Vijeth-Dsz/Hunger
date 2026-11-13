import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { DonationsService } from '../../../../core/services/donations.service';
import { DonorPortal } from './donor-portal';

describe('DonorPortal use-by date', () => {
  it('creates a food donation with the entered use-by date', async () => {
    const service = {
      list: vi.fn().mockReturnValue(of([])),
      needs: vi.fn().mockReturnValue(of([])),
      create: vi.fn().mockReturnValue(of({ id: 'new-donation', status: 'Available' })),
    };

    await TestBed.configureTestingModule({
      imports: [DonorPortal],
      providers: [{ provide: DonationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(DonorPortal);
    fixture.componentRef.setInput('approved', true);
    fixture.detectChanges();

    const page = fixture.componentInstance;
    const pickupFrom = new Date(Date.now() + 60 * 60 * 1000);
    const pickupUntil = new Date(pickupFrom.getTime() + 60 * 60 * 1000);
    const useBy = new Date(pickupUntil.getTime() + 60 * 60 * 1000);
    page.title = 'Fresh meals';
    page.description = 'Packed lunch meals';
    page.quantity = '10';
    page.pickupAddress = 'Community kitchen';
    page.pickupFrom = pickupFrom.toISOString();
    page.pickupUntil = pickupUntil.toISOString();
    page.useBy = useBy.toISOString();

    page.createDonation();

    expect(service.create).toHaveBeenCalledOnce();
    expect(service.create).toHaveBeenCalledWith(expect.objectContaining({ expiresAt: useBy.toISOString() }));
  });

  it('requires a use-by date that covers pickup before sending a food donation to the API', async () => {
    const service = {
      list: vi.fn().mockReturnValue(of([])),
      needs: vi.fn().mockReturnValue(of([])),
      create: vi.fn().mockReturnValue(of({ id: 'new-donation', status: 'Available' })),
    };

    await TestBed.configureTestingModule({
      imports: [DonorPortal],
      providers: [{ provide: DonationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(DonorPortal);
    fixture.componentRef.setInput('approved', true);
    fixture.detectChanges();

    const page = fixture.componentInstance;
    const pickupFrom = new Date(Date.now() + 60 * 60 * 1000);
    page.title = 'Fresh meals';
    page.description = 'Packed lunch meals';
    page.quantity = '10';
    page.pickupAddress = 'Community kitchen';
    page.pickupFrom = pickupFrom.toISOString();
    page.pickupUntil = new Date(pickupFrom.getTime() + 60 * 60 * 1000).toISOString();

    page.createDonation();

    expect(service.create).not.toHaveBeenCalled();
    expect(page.error()).toContain('use-by date after pickup');
  });
});
