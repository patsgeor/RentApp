export interface OverdueContractDto {
  id: string;
  customerName: string;
  outstandingBalance: number;
  endDate: string;
  assetNames: string[];
}

export interface RecentTransactionDto {
  id: string;
  amount: number;
  paymentDate: string;
  transactionType: 0 | 1; // 0=Income 1=Expense
  paymentMethod: 0 | 1 | 2;
  customerName?: string;
  label?: string;
}

export interface MonthlyChartDto {
  month: string;
  income: number;
  expenses: number;
}

export interface KpiDto {
  activeContracts: number;
  newContractsThisMonth: number;
  monthlyIncome: number;
  yearlyIncome: number;          
  totalOutstandingBalance: number;
  availableAssets: number;
  rentedAssets: number;
  totalAssets: number;
}

export interface DashboardDto {
  kpi: KpiDto;
  overdueContracts: OverdueContractDto[];
  recentTransactions: RecentTransactionDto[];
  monthlyChart: MonthlyChartDto[];
  yearlyChart: MonthlyChartDto[];                  
  topAssets: TopAssetDto[];                        
  topCustomers: TopCustomerDto[];                 
  upcomingInstallments: UpcomingInstallmentDto[];  
}

export interface TopAssetDto {
  id: string;
  name: string;
  totalRevenue: number;
  contractCount: number;
}

export interface TopCustomerDto {
  id: string;
  name: string;
  outstandingBalance: number;
  activeContracts: number;
}

export interface UpcomingInstallmentDto {
  month: string;
  expectedAmount: number;
  count: number;
}
