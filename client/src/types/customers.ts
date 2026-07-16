export interface CustomerDto {
   id: string;
  name: string;
  afm: string;
  dou?: string;
  address?: string;
  representative?: string;
  isDeleted: boolean;
  deletedAt?: string;
  createdAt: string;
  contacts: ContactDto[];
  rowVersion?: number;  
}

export interface CreateCustomerDto {
  name: string;
  afm: string;
  phones: string[];
  dou?: string;
  representative?: string;
}

export interface ContactDto {
  id: string;
  name: string;
  phone?: string;
  email?: string;
  canUseAsset: boolean;
  notes?: string;
  rowVersion?: number;  
}

export interface CustomerStatsDto {
  total: number;
  active: number;
  inactive: number;
  newThisMonth: number;
}


export interface UpdateCustomerDto {
  rowVersion?: number;
  name: string;
  afm: string;
  representative?: string;
}

export interface CustomerLookupDto {
  id: string;
  name: string;
  afm: string;
}


export interface AadeCompanyDto {
  afm: string;
  name: string | null;
  nameEn: string | null;
  doy: string | null;
  doyDescription: string | null;
  address: string | null;
  addressNo: string | null;
  zipCode: string | null;
  city: string | null;
  companyType: string | null;
  isActive: boolean;
}
