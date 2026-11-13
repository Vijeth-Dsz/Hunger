import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PortalRole, RegisterRequest, UserRole } from '../../core/models/account.model';
import { HUNGER_COPY } from '../../core/i18n/hunger-copy';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-auth-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './auth-page.html',
  styleUrls: ['./auth-page.css', './auth-page-roles.css'],
})
export class AuthPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly copy = HUNGER_COPY.auth;
  readonly commonCopy = HUNGER_COPY.common;
  readonly registering = signal(false);
  readonly adminLogin = signal(false);
  readonly submitting = signal(false);
  readonly rolesLoading = signal(true);
  readonly roles = signal<PortalRole[]>([]);
  readonly selectedPortal = signal<PortalRole | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly verificationDocument = signal<File | null>(null);
  name = '';
  email = '';
  organizationName = '';
  role: UserRole | '' = '';
  password = '';

  ngOnInit(): void {
    this.adminLogin.set(this.route.snapshot.data['adminLogin'] === true);
    this.auth.roles().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.role = this.adminLogin()
          ? roles.find((option) => option.roleKey === 'Admin')?.roleKey ?? ''
          : roles.find((option) => option.registrationAllowed)?.roleKey ?? '';
        this.rolesLoading.set(false);
      },
      error: () => {
        this.rolesLoading.set(false);
        this.error.set(this.copy.rolesUnavailable);
      },
    });
  }

  toggleMode(): void {
    if (this.adminLogin()) return;
    this.registering.update((value) => !value);
    this.error.set('');
    this.notice.set('');
    this.verificationDocument.set(null);
    if (this.registering() && !this.role) {
      this.role = this.roles().find((option) => option.registrationAllowed)?.roleKey ?? '';
    }
  }

  registrationRoles(): PortalRole[] {
    return this.roles().filter((role) => role.registrationAllowed);
  }

  selectVerificationDocument(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (!file) {
      this.verificationDocument.set(null);
      return;
    }

    const allowedExtensions = ['.pdf', '.jpg', '.jpeg', '.png'];
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!allowedExtensions.includes(extension) || file.size === 0 || file.size > 5 * 1024 * 1024) {
      this.verificationDocument.set(null);
      this.error.set(this.copy.invalidVerificationDocument);
      (event.target as HTMLInputElement).value = '';
      return;
    }

    this.error.set('');
    this.verificationDocument.set(file);
  }

  selectPortal(role: PortalRole): void {
    if (this.adminLogin()) return;
    this.error.set('');
    this.notice.set('');
    this.selectedPortal.set(role);
    this.registering.set(role.registrationAllowed);
    if (role.registrationAllowed) this.role = role.roleKey;
  }

  submit(): void {
    if (this.submitting()) return;
    this.error.set('');
    this.notice.set('');
    if (this.registering() && !this.role) {
      this.error.set(this.commonCopy.chooseRegistrationRole);
      return;
    }
    this.submitting.set(true);
    if (this.registering()) {
      const selectedRole = this.role;
      const document = this.verificationDocument();
      if (!selectedRole) {
        this.submitting.set(false);
        this.error.set(this.commonCopy.chooseRegistrationRole);
        return;
      }
      if (!document) {
        this.submitting.set(false);
        this.error.set(this.copy.verificationDocumentRequired);
        return;
      }
      const request: RegisterRequest = {
        name: this.name.trim(), email: this.email.trim(), organizationName: this.organizationName.trim(),
        role: selectedRole, password: this.password, verificationDocument: document,
      };
      this.auth.register(request).subscribe({
        next: (registeredAccount) => {
          this.auth.login(request.email, request.password).subscribe({
            next: (account) => {
              this.submitting.set(false);
              this.password = '';
              void this.router.navigateByUrl(account.role === 'Admin' ? '/admin' : '/dashboard');
            },
            error: (error: unknown) => {
              this.registering.set(false);
              this.notice.set(registeredAccount.verificationStatus === 'Pending'
                ? this.copy.accountPending
                : this.copy.accountCreated);
              this.handleError(error);
            },
          });
        },
        error: (error: unknown) => this.handleError(error),
      });
    } else {
      this.auth.login(this.email.trim(), this.password).subscribe({
        next: (account) => {
          if (this.adminLogin() && account.role !== 'Admin') {
            this.auth.logout().subscribe({
              next: () => {
                this.submitting.set(false);
                this.password = '';
                this.error.set(this.copy.adminOnly);
              },
              error: (error: unknown) => this.handleError(error),
            });
            return;
          }

          this.submitting.set(false);
          this.password = '';
          void this.router.navigateByUrl(account.role === 'Admin' ? '/admin' : '/dashboard');
        },
        error: (error: unknown) => this.handleError(error),
      });
    }
  }

  private handleError(error: unknown): void {
    this.submitting.set(false);
    if (error instanceof HttpErrorResponse && error.status === 401) {
      this.error.set(this.copy.credentialsInvalid);
      return;
    }
    if (error instanceof HttpErrorResponse && error.status === 409) {
      this.error.set(this.copy.accountExists);
      return;
    }
    this.error.set(error instanceof HttpErrorResponse && error.status === 400
      ? this.commonCopy.invalidRequest
      : this.commonCopy.apiFailure);
  }
}
