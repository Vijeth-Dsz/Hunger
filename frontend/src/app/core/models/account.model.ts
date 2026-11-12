export type UserRole = 'Donor' | 'Recipient' | 'Admin';
export type VerificationStatus = 'Pending' | 'Approved';

export interface Account {
  id: string;
  name: string;
  email: string;
  organizationName: string;
  role: UserRole;
  verificationStatus: VerificationStatus;
}

export interface RegisterRequest {
  name: string;
  email: string;
  organizationName: string;
  role: 'Donor' | 'Recipient';
  password: string;
}
