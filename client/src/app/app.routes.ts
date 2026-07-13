import { Routes } from '@angular/router';
import { Login } from '../features/user/login/login';
import { RegisterTenant } from '../features/user/register-tenant/register-tenant';
import { authGuard } from '../core/guards/auth-guard';
import { Home } from '../home/home/home';
import { LandingPage } from '../home/landing-page/landing-page';
import { guestGuard } from '../core/guards/guest-guard';
import { CustomerList } from '../features/customers/customer-list/customer-list';
import { CustomerForm } from '../features/customers/customer-form/customer-form';
import { ServerError } from '../shared/errors/server-error/server-error';
import { NotFound } from '../shared/errors/not-found/not-found';
import { MemberRegister } from '../features/user/member-register/member-register';
import { MemberInvite } from '../features/user/member-invite/member-invite';
import { AssetList } from '../features/asset/asset-list/asset-list';
import { AssetForm } from '../features/asset/asset-form/asset-form';
import { AssetDetail } from '../features/asset/asset-detail/asset-detail';
import { QrScanner } from '../shared/qr-scanner/qr-scanner';
import { PaymentPage } from '../features/payment/payment-page/payment-page';
import { AssetCategoryList } from '../features/category/asset-category-list/asset-category-list';
import { AssetCategoryDetail } from '../features/category/asset-category-detail/asset-category-detail';
import { IncomeForm } from '../features/transactions/income-form/income-form';
import { ExpenseForm } from '../features/transactions/expense-form/expense-form';
import { TransactionList } from '../features/transactions/transaction-list/transaction-list';
import { Dashboard } from '../home/dashboard/dashboard';
import { ContractList } from '../features/contract/contract-list/contract-list';
import { ContractForm } from '../features/contract/contract-form/contract-form';
import { DebtMonitor } from '../features/debts/debt-monitor/debt-monitor';
import { ContractInstallments } from '../features/contract/contract-installments/contract-installments';

export const routes: Routes = [
  {path: '',component: LandingPage,canActivate: [guestGuard],runGuardsAndResolvers: 'always'},
 {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [guestGuard],
    children: [
      {path: 'landing',component: LandingPage},
      {path: 'login',component: Login},
      {path: 'register', component: RegisterTenant},
      {path: 'register-invite', component: MemberRegister },


    ]
  },
  
  // αφου εχει κανει login
   {
    path: '',
    component: Home,
    runGuardsAndResolvers: 'always',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },  
      { path: 'home', component: Dashboard },    
      { path: 'dashboard', component: Dashboard }, 
      
      { path: 'customer', component: CustomerList },
      { path: 'customer/new', component: CustomerForm },
      { path: 'customer/:id/edit', component: CustomerForm },
      { path: 'customer/:id', component: CustomerList },

      { path: 'assets', component: AssetList },
      { path: 'assets/new', component: AssetForm },
      { path: 'assets/:id/edit', component: AssetForm },
      { path: 'assets/:id', component: AssetDetail },

      { path: 'asset-categories', component: AssetCategoryList },
      { path: 'asset-categories/:id', component: AssetCategoryDetail },

      // Legacy (keep for now)
      { path: 'payments', component: PaymentPage },
      { path: 'payments/new', component: PaymentPage },
      // New transactions section
      { path: 'transactions', component: IncomeForm },
      { path: 'transactions/income/new', component: IncomeForm },
      { path: 'transactions/expense/new', component: ExpenseForm },
      { path: 'transactions/history', component: TransactionList },

      { path: 'contracts', component: ContractList },
      { path: 'contracts/new', component: ContractForm },
      { path: 'contracts/:id/edit', component: ContractForm },

      { path: 'contracts/:id/installments', component: ContractInstallments },
      { path: 'debts', component: DebtMonitor },

      { path: 'scan', component: QrScanner },


      { path: 'invite', component: MemberInvite }
    ]
   }, 
  {path:'server-error',component:ServerError},
  {path:'**',component:NotFound}
];