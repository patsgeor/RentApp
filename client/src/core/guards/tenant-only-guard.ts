import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account-service';

/**
 * Ο SuperAdmin διαχειρίζεται την πλατφόρμα, δεν δουλεύει μέσα σε tenant.
 * Τα δεδομένα του (πάγια, συμβόλαια, εισπράξεις) ανήκουν στον System tenant
 * και δεν έχουν νόημα — τον στέλνουμε στο δικό του dashboard.
 */
export const tenantOnlyGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  const router = inject(Router);

  if (accountService.currentUser()?.roles?.includes('SuperAdmin')) {
    return router.createUrlTree(['/admin/dashboard']);
  }

  return true;
};
