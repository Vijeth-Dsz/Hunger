export type DonationType = 'PreparedFood' | 'Groceries' | 'Stationery';
export type DonationStatus = 'Available' | 'Requested' | 'Accepted' | 'PickedUp' | 'Delivered' | 'Cancelled';
export type ReviewStatus = 'Pending' | 'Approved' | 'Rejected';
export type NeedStatus = 'Open' | 'PartiallyFulfilled' | 'Fulfilled' | 'Closed';
export type ContributionStatus = 'PendingReview' | 'Approved' | 'Delivered' | 'Rejected' | 'Cancelled';

export interface Donation {
  id: string;
  createdBy: string;
  recipientId: string | null;
  title: string;
  description: string;
  type: DonationType;
  quantity: string;
  unit: string | null;
  pickupAddress: string;
  pickupFrom: string;
  pickupUntil: string;
  storageInstructions: string | null;
  allergens: string | null;
  status: DonationStatus;
  createdAt: string;
  donorOrganization: string;
  recipientOrganization: string;
  expiresAt: string | null;
  reviewStatus: ReviewStatus;
}

export interface CreateDonation {
  title: string;
  description: string;
  type: DonationType;
  quantity: string;
  unit: string;
  pickupAddress: string;
  pickupFrom: string;
  pickupUntil: string;
  storageInstructions: string;
  allergens: string;
  expiresAt: string | null;
}

export interface CreateNeed {
  title: string;
  description: string;
  type: DonationType;
  quantity: number;
  unit: string;
  neededBy: string;
}

export interface CommunityNeed extends CreateNeed {
  id: string;
  recipientId: string;
  status: NeedStatus;
  reviewStatus: ReviewStatus;
  fulfilledQuantity: number;
  recipientOrganization: string;
  createdAt: string;
}

export interface CreateContribution {
  quantity: number;
  unit: string;
  pickupAddress: string;
  pickupFrom: string;
  pickupUntil: string;
  expiresAt: string | null;
  storageInstructions: string;
  allergens: string;
}

export interface NeedContribution extends CreateContribution {
  id: string;
  needId: string;
  donorId: string;
  status: ContributionStatus;
  donorOrganization: string;
  recipientOrganization: string;
  needTitle: string;
  createdAt: string;
}

export interface AdminAccount {
  id: string;
  name: string;
  email: string;
  organizationName: string;
  role: 'Donor' | 'Recipient';
  verificationStatus: 'Pending' | 'Approved' | 'Rejected';
  createdAt: string;
  hasVerificationDocument: boolean;
}

export interface DashboardSummary {
  total: number;
  active: number;
  accepted: number;
  delivered: number;
}

export interface StatusHistoryItem {
  status: DonationStatus;
  changedAt: string;
  changedBy: string;
}
