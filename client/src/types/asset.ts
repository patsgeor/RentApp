export enum RateUnit { PerHour = 0, PerDay = 1, PerMonth = 2, Sale = 3 }
export enum AssetStatus { Active = 0, Retired = 1, UnderMaintenance = 2, Damaged = 3 }
export enum FieldDataType { Text = 0, Number = 1, Boolean = 2, Date = 3, DateTime = 4 }
export enum RentalStatus { Pending = 0, Active = 1, Completed = 2, Cancelled = 3 }


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
export interface PhotoDto {
  id: string;
  url: string;
  isMain: boolean;
}

export interface AssetDto {
  id: string;
  assetTypeId: string;
  assetTypeName: string;
  name: string;
  notes?: string;
  rateUnit: RateUnit;
  cost: number;
  status: AssetStatus;
  createdAt: string;
  photoUrl?: string;
  rowVersion?: number;  
  attributes: Record<string, unknown>;
}

export interface AssetDetailDto extends AssetDto {
  photos: PhotoDto[];
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
  rateUnit: RateUnit;
  cost: number;
  attributes: Record<string, unknown>;
}

export interface AssetUpdateDto {
  rowVersion?: number;  
  name: string;
  notes?: string;
  rateUnit: RateUnit;
  status: AssetStatus;
  cost: number;
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
  rowVersion?: number;  
}

export interface CostAssetHistCreateDto {
  date: string;
  description: string;
  cost: number;
  maintainedBy?: string;
}


export interface CostAssetHistUpdateDto {
  date: string;
  description: string;
  cost: number;
  maintainedBy?: string;
  rowVersion?: number;
}


export interface AssetContractHistDto {
  contractId: string;
  customerName: string;
  startDate: string;
  endDate: string;
  status: RentalStatus;
  totalAmount: number;
  notes?: string;
  rowVersion?: number;  
}


export interface AssetTypeCreateDto {
  name: string;
  description?: string;
}

export interface AssetTypeUpdateDto {
  name: string;
  description?: string;
  rowVersion?: number;
}

export interface AssetTypeFieldOptionCreateDto {
  label: string;
  value: string;
  displayOrder: number;
}

export interface AssetTypeFieldOptionUpdateDto {
  label: string;
  value: string;
  displayOrder: number;
  rowVersion?: number;  
}

export interface AssetTypeFieldCreateDto {
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
  options?: AssetTypeFieldOptionCreateDto[];
}

export interface AssetTypeFieldUpdateDto {
  label: string;
  placeholder?: string;
  defaultValue?: string;
  displayOrder: number;
  validationRegex?: string;
  minValue?: number;
  maxValue?: number;
  isRequired: boolean;
  rowVersion?: number;  
}


export interface AssetAttributeFilter {
  fieldName: string;
  equals?: string;
  minValue?: number;
  maxValue?: number;
}

export interface AssetSearchRequest {
  assetTypeId: string;
  status?: number;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  filters: AssetAttributeFilter[];
}

export interface AssetContractPeriodDto {
  contractId: string;
  customerName: string;
  startDate: string;
  endDate: string;
  status: number;
}

export interface AssetAvailabilityDto {
  isAvailable: boolean;
  conflicts: AssetContractPeriodDto[];
}

export interface AssetCalendarParams {
  from: string;
  to: string;
}

export interface AssetCalendarEntryDto {
  assetId: string;
  assetName: string;
  periods: AssetContractPeriodDto[];
}