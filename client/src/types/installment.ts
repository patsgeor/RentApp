export enum InstallmentStatus {
  Pending       = 0,
  PartiallyPaid = 1,
  Paid          = 2,
  Overdue       = 3,
  Cancelled     = 4,
}

export interface AllocationSummaryDto {
  allocationId:    string;
  paymentId:       string;
  paymentDate:     string;
  allocatedAmount: number;
}

export interface InstallmentDto {
  id:                    string;
  contractId:            string;
  contractReferenceCode?: string;
  customerName:          string;
  installmentNumber:     number;
  periodStart:           string;
  periodEnd:             string;
  dueDate:               string;
  amount:                number;
  taxAmount:             number;
  totalAmount:           number;
  allocatedAmount:       number;
  outstandingAmount:     number;
  status:                InstallmentStatus;
  notes?:                string;
  allocations:           AllocationSummaryDto[];
}

export interface DebtFilterParams {
  pageNumber?:  number;
  pageSize?:    number;
  month?:       number;
  year?:        number;
  status?:      InstallmentStatus;
  customerId?:  string;
  contractId?:  string;
  search?:      string;
}

export interface EditLine {
  id?:               string;
  installmentNumber: number;
  periodStart:       string; // 'YYYY-MM-DD'
  periodEnd:         string;
  dueDate:           string;
  amount:            number;
  taxAmount:         number;
  notes:             string;
  // read-only from server
  allocatedAmount:   number;
  status:            InstallmentStatus;
}

export interface ScheduleInstallmentItem {
  id?:               string;
  installmentNumber: number;
  periodStart:       string;
  periodEnd:         string;
  dueDate:           string;
  amount:            number;
  taxAmount:         number;
  notes?:            string;
}

export interface DebtStatsDto {
  expectedThisMonth:  number;
  totalOutstanding:   number;
  overdueCount:       number;
  overdueAmount:      number;
  pendingCount:       number;
  partiallyPaidCount: number;
}