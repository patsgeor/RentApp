export enum AcquisitionType { Purchase = 0, Leasing = 1 }
export enum AssetStatus { Available = 0, Rented = 1, UnderMaintenance = 2, Damaged = 3 }
export enum FieldDataType { Text = 0, Number = 1, Boolean = 2, Date = 3, DateTime = 4 }

export interface AssetTypeFieldOptionDto {
  id: string;
  label: string;
  value: string;
  displayOrder: number;
}

export interface AssetTypeFieldDto {
  id: string;
  name: string;
  label: string;
  dataType: FieldDataType;
  placeholder?: string;
  defaultValue?: string;
  displayOrder: number;
  validationRegex?: string;
  minValue?: number;
  maxValue?: number;
  isRequired: boolean;
  options: AssetTypeFieldOptionDto[];
}

export interface AssetTypeDto {
  id: string;
  name: string;
  description?: string;
  assetCount: number;
  fields: AssetTypeFieldDto[];
}

export interface AssetTypeLookupDto {
  id: string;
  name: string;
}

export interface AssetDto {
  id: string;
  assetTypeId: string;
  assetTypeName: string;
  name: string;
  notes?: string;
  acquisitionType: AcquisitionType;
  acquisitionCost: number;
  monthlyLeaseCost?: number;
  status: AssetStatus;
  createdAt: string;
  attributes: Record<string, unknown>;
}

export interface AssetLookupDto {
  id: string;
  name: string;
  status: AssetStatus;
}

export interface AssetCreateDto {
  assetTypeId: string;
  name: string;
  notes?: string;
  acquisitionType: AcquisitionType;
  acquisitionCost: number;
  monthlyLeaseCost?: number;
  attributes: Record<string, unknown>;
}

export interface AssetUpdateDto {
  name: string;
  notes?: string;
  acquisitionType: AcquisitionType;
  acquisitionCost: number;
  monthlyLeaseCost?: number;
  attributes: Record<string, unknown>;
}

export interface AssetStatusUpdateDto {
  status: AssetStatus;
}

export interface CostAssetHistDto {
  id: string;
  date: string;
  description: string;
  cost: number;
  maintainedBy?: string;
}

export interface CostAssetHistCreateDto {
  date: string;
  description: string;
  cost: number;
  maintainedBy?: string;
}
