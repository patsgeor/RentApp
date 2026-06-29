import { Routes } from '@angular/router';
import { Login } from '../features/user/login/login';
import { RegisterTenant } from '../features/user/register-tenant/register-tenant';
import { authGuard } from '../core/guards/auth-guard';
import { InviteMember } from '../features/user/invite-member/invite-member';
import { Home } from '../home/home/home';
import { LandingPage } from '../home/landing-page/landing-page';
import { guestGuard } from '../core/guards/guest-guard';

export const routes: Routes = [
 {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [guestGuard],
    children: [
      {
        path: 'landing',
        component: LandingPage
      },
      {
        path: 'login',
        component: Login
      },
      {
        path: 'register',
        component: RegisterTenant
      }
    ]
  },
  
  // αφου εχει κανει login
   {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [authGuard],
    children: [
      {
        path: 'home',
        component: Home
      },
      { path: 'InviteMember',  component: InviteMember}
    ]
   }, 
  { path: '**', redirectTo: '' }
];