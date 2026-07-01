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
      { path: 'home', component: Home },
      
      { path: 'customer', component: CustomerList },
      { path: 'customer/new', component: CustomerForm },
      { path: 'customer/:id/edit', component: CustomerForm },
      { path: 'customer/:id', component: CustomerList },

      { path: 'assets', component: AssetList },
      { path: 'assets/new', component: AssetForm },
      { path: 'assets/:id/edit', component: AssetForm },
      { path: 'assets/:id', component: AssetDetail },

      { path: 'invite', component: MemberInvite }
    ]
   }, 
  {path:'server-error',component:ServerError},
  {path:'**',component:NotFound}
];