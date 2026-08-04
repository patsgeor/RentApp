import { PlanType } from './user';

export enum SubscriptionStatus { Trial = 0, Active = 1, Suspended = 2, Cancelled = 3 }

export interface TenantAdminDto {
  id: string;
  name: string;
  vatNumber?: string;
  contactInfo?: string;
  planType: PlanType;
  planExpiresAt?: string;
  subscriptionStatus: SubscriptionStatus;
  userCount: number;
}

export { PlanType };
