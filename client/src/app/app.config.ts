import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection, LOCALE_ID } from '@angular/core';

import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { HttpRequest, provideHttpClient, withInterceptors } from '@angular/common/http';
import { errorsInterceptor } from '../core/interceptors/errors-interceptor';
import { authInterceptor } from '../core/interceptors/auth-interceptor';
import { registerLocaleData, DATE_PIPE_DEFAULT_OPTIONS } from '@angular/common';
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
    { provide: LOCALE_ID, useValue: 'el' },
    // Το API στέλνει τοπική ώρα (Ελλάδας) με ετικέτα UTC χωρίς πραγματική μετατροπή —
    // το DatePipe πρέπει να τη διαβάζει σαν 'UTC' (κυριολεκτικά), αλλιώς την ξαναμετατρέπει
    // στη ζώνη του browser και οι ώρες/ημερομηνίες μετατοπίζονται (π.χ. περνάνε τα μεσάνυχτα).
    { provide: DATE_PIPE_DEFAULT_OPTIONS, useValue: { timezone: 'UTC' } }
  ]
};
