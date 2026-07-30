import { RentalStatus } from './asset';

export enum PaymentMethod { Cash = 0, Card = 1, BankTransfer = 2 }
export enum TransactionType { Income = 0, Expense = 1 }
export enum PaymentMatchStatus { Unmatched = 0, AutoMatched = 1, ManuallyMatched = 2 }


export interface ContractPaymentDto {
  id: string;
  customerName: string;
  startDate: string;
  endDate: string;
  totalAmount: number;
  paidAmount: number;
  outstandingBalance: number;
  status: RentalStatus;
  canExtend: boolean;
  assetNames: string[];
  referenceCode?: string;
}

export interface AllocationItemDto {
  installmentId: string;
  amount: number;
  notes?: string;
}

export interface IncomeCreateDto {
  amount: number;
  paymentDate: string;
  paymentMethod: PaymentMethod;
  notes?: string;
  tenantReferenceCode?: string;
  allocations?: AllocationItemDto[];
}

export interface ExpenseCreateDto {
  amount: number;
  paymentDate: string;
  paymentMethod: PaymentMethod;
  description: string;
  notes?: string;
  assetIds?: string[];
}

export interface PaymentAllocationDto {
  allocationId: string;
  installmentId: string;
  contractReferenceCode?: string;
  installmentNumber: number;
  dueDate: string;
  allocatedAmount: number;
}

export interface PaymentListItemDto {
  id: string;
  amount: number;
  unallocatedAmount: number;
  paymentDate: string;
  paymentMethod: PaymentMethod;
  transactionType: TransactionType;
  matchStatus: PaymentMatchStatus;
  notes?: string;
  description?: string;
  tenantReferenceCode?: string;
  contractReferences?: string[];
  customerName?: string;
  allocations: PaymentAllocationDto[];
  assetNames?: string[];
  attachmentUrl?: string;
  attachmentFileName?: string;
  createdAt: string;
}
