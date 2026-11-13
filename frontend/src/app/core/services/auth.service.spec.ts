import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends registration fields and proof as multipart form data without setting a content type', () => {
    const proof = new File(['%PDF-1.7'], 'proof.pdf', { type: 'application/pdf' });
    service.register({
      name: 'Test Donor',
      email: 'donor@example.org',
      organizationName: 'Test Kitchen',
      role: 'Donor',
      password: 'TestPassword123!',
      verificationDocument: proof,
    }).subscribe();

    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeInstanceOf(FormData);
    expect(request.request.headers.has('Content-Type')).toBe(false);
    expect(request.request.body.get('verificationDocument')).toMatchObject({ name: proof.name, type: proof.type });
    request.flush({
      id: 'account-id',
      name: 'Test Donor',
      email: 'donor@example.org',
      organizationName: 'Test Kitchen',
      role: 'Donor',
      verificationStatus: 'Pending',
    });
  });
});
