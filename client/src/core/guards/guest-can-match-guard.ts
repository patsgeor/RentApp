import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { AccountService } from '../services/account-service';

export const guestCanMatchGuard: CanActivateFn = (route, state) => {
  return !inject(AccountService).isLoggedIn();
};
