import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { AccountService } from '../services/account-service';
import { catchError, switchMap, throwError } from 'rxjs';


export const authInterceptor: HttpInterceptorFn = (req, next) => {
     const accountService = inject(AccountService);

  const addToken = (r: HttpRequest<unknown>) => {
    const token = accountService.currentUser()?.token;
    console.log(token);
    return token ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;
  };

  return next(addToken(req)).pipe(
    catchError((err: HttpErrorResponse) => {
      // Αν δεν είναι 401 ή αφορά ήδη το refresh/login, πέταξε το error
      if (err.status !== 401 || req.url.includes('refresh-token') || req.url.includes('login')) {
        return throwError(() => err);
      }
      // Δοκίμασε refresh
      return accountService.refreshToken().pipe(
        switchMap(() => next(addToken(req))),  // retry με νέο token
        catchError(() => {
          accountService.logout();             // refresh απέτυχε → logout
          return throwError(() => err);
        })
      );
    })
  );
};