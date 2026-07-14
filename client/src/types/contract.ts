import { RateUnit, RentalStatus } from './asset';

export { RateUnit, RentalStatus };

export enum InstallmentFrequency { Monthly = 0, Weekly = 1, Quarterly = 2, Yearly = 3, OneTime = 4 }

export interface ContractListItemDto {
  id: string;
  customerName: string;
  referenceCode?: string;
  startDate: string;
  endDate: string;
  totalAmount: number;
  paidAmount: number;
  outstandingBalance: number;
  status: RentalStatus;
  assetNames: string[];
}

export interface ContractDetailDto {
  id: string;
  rowVersion: number;
  customerId: string;
  customerName: string;
  referenceCode?: string;
  startDate: string;
  endDate: string;
  signedDate?: string;
  totalAmount: number;
  taxAmount: number;
  discountAmount: number;
  status: RentalStatus;
  installmentFrequency: InstallmentFrequency;
  notes?: string;
  terms?: string;
  assets: ContractAssetDto[];
}

export interface ContractAssetDto {
  id: string;
  assetId: string;
  assetName: string;
  startDate: string;
  endDate: string;
  unitCost: number;
  rateUnit: RateUnit;
  calculatedAmount: number;
  notes?: string;
}

export interface AvailableAssetDto {
  id: string;
  name: string;
  assetTypeName?: string;
  cost: number;
  rateUnit: RateUnit;
}

export interface ContractAssetLineItem {
  assetId: string;
  assetName: string;
  assetTypeName?: string;
  startDate: string;
  endDate: string;
  unitCost: number;
  rateUnit: RateUnit;
  calculatedAmount: number;
  notes: string;
}

export interface ContractAssetCreateDto {
  assetId: string;
  startDate: string;
  endDate: string;
  unitCost: number;
  rateUnit: RateUnit;
  calculatedAmount: number;
  notes?: string;
}

export interface ContractCreateDto {
  customerId: string;
  startDate: string;
  endDate: string;
  signedDate?: string;
  referenceCode?: string;
  taxAmount: number;
  discountAmount: number;
  installmentFrequency: InstallmentFrequency;
  notes?: string;
  terms?: string;
  assets: ContractAssetCreateDto[];
}

export interface ContractUpdateDto extends ContractCreateDto {
  rowVersion: number;
  status: RentalStatus;
}
