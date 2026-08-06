export enum ReportDataset {
  Kpi = 0,
  Income = 1,
  Expenses = 2,
  Contracts = 3,
  Assets = 4,
  Installments = 5,
  Customers = 6,
}

/** «Πάγια της περιόδου» είναι διφορούμενο — ο χρήστης επιλέγει ρητά τη σημασία. */
export enum AssetPeriodMode {
  Registered = 0,
  Rented = 1,
}

export interface ReportRequestDto {
  dateFrom: string;
  dateTo: string;
  datasets: ReportDataset[];
  assetMode: AssetPeriodMode;
}

export interface ReportPreviewRowDto {
  dataset: ReportDataset;
  label: string;
  rowCount: number;
  exceedsSheetLimit: boolean;
}

export interface ReportPreviewDto {
  rows: ReportPreviewRowDto[];
  totalRows: number;
  maxRowsPerSheet: number;
  maxRowsTotal: number;
  exceedsLimit: boolean;
  message?: string | null;
}
