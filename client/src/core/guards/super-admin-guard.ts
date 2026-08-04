import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account-service';

export const superAdminGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  const router = inject(Router);

  if (accountService.currentUser()?.roles?.includes('SuperAdmin')) {
    return true;
  }

  return router.createUrlTree(['/dashboard']);
};
