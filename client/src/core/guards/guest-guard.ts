import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account-service';

export const guestGuard: CanActivateFn = (route, state) => {
const account = inject(AccountService);
  const router = inject(Router);

  if (account.isLoggedIn()) {
    return router.createUrlTree(['/home']);
  }

  return true;
};
