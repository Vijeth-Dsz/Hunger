import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth.service';
import { AuthPage } from './auth-page';

describe('AuthPage', () => {
  it('signs a new donor in and opens the dashboard after registration', async () => {
    const account = {
      id: 'account-id',
      name: 'Test Donor',
      email: 'donor@example.org',
      organizationName: 'Test Kitchen',
      role: 'Donor' as const,
      verificationStatus: 'Approved' as const,
    };
    const auth = {
      roles: vi.fn().mockReturnValue(of([
        { roleKey: 'Donor' as const, displayName: 'Donor', description: 'Share food', registrationAllowed: true },
        { roleKey: 'Recipient' as const, displayName: 'Recipient', description: 'Request support', registrationAllowed: true },
        { roleKey: 'Admin' as const, displayName: 'Admin', description: 'Review activity', registrationAllowed: false },
      ])),
      register: vi.fn().mockReturnValue(of(account)),
      login: vi.fn().mockReturnValue(of(account)),
    };

    await TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [
        { provide: AuthService, useValue: auth },
        provideRouter([]),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AuthPage);
    fixture.detectChanges();
    const page = fixture.componentInstance;
    page.registering.set(true);
    page.name = account.name;
    page.email = account.email;
    page.organizationName = account.organizationName;
    page.password = 'TestPassword123!';
    const verificationDocument = new File(['%PDF-1.7'], 'organization-proof.pdf', { type: 'application/pdf' });
    page.verificationDocument.set(verificationDocument);
    const navigation = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    page.submit();

    expect(auth.register).toHaveBeenCalledOnce();
    expect(auth.register).toHaveBeenCalledWith(expect.objectContaining({ verificationDocument }));
    expect(auth.login).toHaveBeenCalledWith(account.email, 'TestPassword123!');
    expect(navigation).toHaveBeenCalledWith('/dashboard');
    expect(page.submitting()).toBe(false);
  });

  it('rejects non-admin accounts on the dedicated admin sign-in route', async () => {
    const donor = {
      id: 'donor-id',
      name: 'Test Donor',
      email: 'donor@example.org',
      organizationName: 'Test Kitchen',
      role: 'Donor' as const,
      verificationStatus: 'Approved' as const,
    };
    const auth = {
      roles: vi.fn().mockReturnValue(of([
        { roleKey: 'Admin' as const, displayName: 'Admin', description: 'Review activity', registrationAllowed: false },
      ])),
      login: vi.fn().mockReturnValue(of(donor)),
      logout: vi.fn().mockReturnValue(of(void 0)),
    };

    await TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [
        { provide: AuthService, useValue: auth },
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { adminLogin: true } } } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AuthPage);
    fixture.detectChanges();
    const page = fixture.componentInstance;
    page.email = donor.email;
    page.password = 'TestPassword123!';
    const navigation = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    page.submit();

    expect(page.adminLogin()).toBe(true);
    expect(auth.logout).toHaveBeenCalledOnce();
    expect(page.error()).toBe(page.copy.adminOnly);
    expect(navigation).not.toHaveBeenCalled();
  });

  it('routes an administrator to the administration dashboard after sign-in', async () => {
    const admin = {
      id: 'admin-id',
      name: 'Test Admin',
      email: 'admin@example.org',
      organizationName: 'Hunger',
      role: 'Admin' as const,
      verificationStatus: 'Approved' as const,
    };
    const auth = {
      roles: vi.fn().mockReturnValue(of([
        { roleKey: 'Admin' as const, displayName: 'Admin', description: 'Review activity', registrationAllowed: false },
      ])),
      login: vi.fn().mockReturnValue(of(admin)),
    };

    await TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [
        { provide: AuthService, useValue: auth },
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { adminLogin: true } } } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AuthPage);
    fixture.detectChanges();
    const page = fixture.componentInstance;
    page.email = admin.email;
    page.password = 'TestPassword123!';
    const navigation = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    page.submit();

    expect(navigation).toHaveBeenCalledWith('/admin');
  });
});
