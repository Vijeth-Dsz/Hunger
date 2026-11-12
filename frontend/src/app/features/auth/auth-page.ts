import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Account, RegisterRequest } from '../../core/models/account.model';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-auth-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.css',
})
export class AuthPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly registering = signal(false);
  readonly submitting = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  name = '';
  email = '';
  organizationName = '';
  role: 'Donor' | 'Recipient' = 'Donor';
  password = '';

  toggleMode(): void {
    this.registering.update((value) => !value);
    this.error.set('');
    this.notice.set('');
  }

  submit(): void {
    if (this.submitting()) return;
    this.error.set('');
    this.notice.set('');
    this.submitting.set(true);
    if (this.registering()) {
      const request: RegisterRequest = {
        name: this.name.trim(), email: this.email.trim(), organizationName: this.organizationName.trim(),
        role: this.role, password: this.password,
      };
      this.auth.register(request).subscribe({
        next: (account) => {
          this.submitting.set(false);
          this.registering.set(false);
          this.password = '';
          this.notice.set(account.verificationStatus === 'Pending'
            ? 'Your recipient account is registered and waiting for organization verification. You can sign in to check its status.'
            : 'Your account is ready. Sign in to continue.');
        },
        error: (error: unknown) => this.handleError(error),
      });
    } else {
      this.auth.login(this.email.trim(), this.password).subscribe({
        next: (account: Account) => {
          this.submitting.set(false);
          void this.router.navigateByUrl(account.role === 'Admin' ? '/admin' : '/dashboard');
        },
        error: (error: unknown) => this.handleError(error),
      });
    }
  }

  private handleError(error: unknown): void {
    this.submitting.set(false);
    if (error instanceof HttpErrorResponse && error.status === 401) {
      this.error.set('The email or password is incorrect.');
      return;
    }
    if (error instanceof HttpErrorResponse && error.status === 409) {
      this.error.set('An account with this email already exists. Try signing in instead.');
      return;
    }
    const message = error instanceof HttpErrorResponse && typeof error.error === 'object' && error.error !== null && 'error' in error.error && typeof error.error.error === 'string'
      ? error.error.error
      : 'Could not reach Hunger. Check the API connection and try again.';
    this.error.set(message);
  }
}
