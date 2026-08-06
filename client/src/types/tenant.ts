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

  assetCount: number;
  customerCount: number;
  contractCount: number;
  transactionCount: number;
  fileCount: number;
  totalIncome: number;
  totalExpenses: number;
  totalRecords: number;
  lastActivity?: string;
}

export interface TenantUserDto {
  id: string;
  email?: string;
  displayName: string;
  isActive: boolean;
  emailConfirmed: boolean;
  roles: string[];
}

export interface AuditLogDto {
  id: string;
  tenantId: string;
  tenantName?: string;
  tableName: string;
  recordId: string;
  action: string;
  userId?: string;
  timestamp: string;
}

export interface ErrorLogDto {
  id: string;
  tenantId?: string;
  tenantName?: string;
  userId?: string;
  method: string;
  path: string;
  statusCode: number;
  message: string;
  exceptionType: string;
  stackTrace?: string;
  timestamp: string;
}

export interface PlanBreakdownDto {
  planType: PlanType;
  count: number;
}

export interface PlatformSummaryDto {
  totalTenants: number;
  activeTenants: number;
  trialTenants: number;
  suspendedTenants: number;
  totalUsers: number;
  totalAssets: number;
  totalContracts: number;
  totalCustomers: number;
  platformIncome: number;
  platformExpenses: number;
  planBreakdown: PlanBreakdownDto[];
  topTenants: TenantAdminDto[];
}

export { PlanType };
