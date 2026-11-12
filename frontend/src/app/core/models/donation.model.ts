export type DonationType = 'PreparedFood' | 'Groceries';
export type DonationStatus = 'Available' | 'Requested' | 'Accepted' | 'PickedUp' | 'Delivered' | 'Cancelled';

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
}

export interface DashboardSummary {
  total: number;
  active: number;
  delivered: number;
}

export interface PendingRecipient {
  id: string;
  name: string;
  email: string;
  organizationName: string;
  createdAt: string;
}

export interface StatusHistoryItem {
  status: DonationStatus;
  changedAt: string;
  changedBy: string;
}
