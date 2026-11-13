export type UserRole = 'Donor' | 'Recipient' | 'Admin';
export type VerificationStatus = 'Pending' | 'Approved' | 'Rejected';

export interface Account {
  id: string;
  name: string;
  email: string;
  organizationName: string;
  role: UserRole;
  verificationStatus: VerificationStatus;
}

export interface PortalRole {
  roleKey: UserRole;
  displayName: string;
  description: string;
  registrationAllowed: boolean;
}

export interface RegisterRequest {
  name: string;
  email: string;
  organizationName: string;
  role: UserRole;
  password: string;
  verificationDocument: File;
}
