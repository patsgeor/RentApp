import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection, LOCALE_ID } from '@angular/core';

import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { HttpRequest, provideHttpClient, withInterceptors } from '@angular/common/http';
import { errorsInterceptor } from '../core/interceptors/errors-interceptor';
import { authInterceptor } from '../core/interceptors/auth-interceptor';
import { registerLocaleData } from '@angular/common';
import localeEl from '@angular/common/locales/el';

registerLocaleData(localeEl);


export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([
        errorsInterceptor,
        authInterceptor
      ])
    ),
   provideZonelessChangeDetection(),
    { provide: LOCALE_ID, useValue: 'el' }    ]
};
