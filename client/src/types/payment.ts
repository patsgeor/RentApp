import { RentalStatus } from './asset';

export enum PaymentMethod { Cash = 0, Card = 1, BankTransfer = 2 }
export enum TransactionType { Income = 0, Expense = 1 }

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
  aadeNumber?: string;
}

export interface IncomeCreateDto {
  contractId: string;
  amount: number;
  paymentDate: string;
  paymentMethod: PaymentMethod;
  notes?: string;
}

export interface ExpenseCreateDto {
  amount: number;
  paymentDate: string;
  paymentMethod: PaymentMethod;
  description: string;
  notes?: string;
  assetIds?: string[];
}

export interface PaymentListItemDto {
  id: string;
  amount: number;
  paymentDate: string;
  paymentMethod: PaymentMethod;
  transactionType: TransactionType;
  notes?: string;
  description?: string;
  contractId?: string;
  customerName?: string;
  assetNames?: string[];
  attachmentUrl?: string;
  attachmentFileName?: string;
  createdAt: string;
}
