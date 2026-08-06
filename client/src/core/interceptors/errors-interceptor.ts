import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastService } from '../services/toast-service';
import { NavigationExtras, Router } from '@angular/router';
import { catchError } from 'rxjs';

export const errorsInterceptor: HttpInterceptorFn = (req, next) => {
  const toast=inject(ToastService);// υπηρεσία για εμφάνιση μηνυμάτων στον χρήστη
  const router=inject(Router);
  
  // Προωθεί το αίτημα και παγιδεύει τυχόν σφάλματα που επιστρέφονται από τον server
  return next(req).pipe(
    //πιανω τα λαθη που επιστρεφει ο server
    catchError( error => {
      if (error) {
        switch(error.status){
          case 400:
            //Ελέγχει αν υπάρχουν validation errors
             if(error.error.errors){
              //δημιουργεί έναν πίνακα για να αποθηκεύσει τα μηνύματα σφαλμάτων
              const modalStateErrors=[];
              
              //Βρόχος για την συλλογή όλων των μηνυμάτων σφαλμάτων από το αντικείμενο errors
              for(const key in error.error.errors){
                if(error.error.errors[key]){
                  modalStateErrors.push(error.error.errors[key]);
                }
              }
              throw modalStateErrors.flat();//επιστρέφει έναν πίνακα με όλα τα σφάλματα
            }else{
              // εμφανιζει το μηνυμα λαθους αν υπαρχει αλλιως εμφανιζει το 'Bad Request'
              toast.error(error.error || 'Bad Request');
            }
            break;
          case 401:
            if (!req.url.includes('refresh-token')) {
              toast.error('Unauthorized');
            }
            break;
          case 404:
            //toast.error('Not Found');
            // Η αναζήτηση πρόσκλησης χειρίζεται μόνη της το «άκυρη ή ληγμένη
            // πρόσκληση» με δικό της, κατανοητό μήνυμα. Χωρίς την εξαίρεση, ο
            // προσκεκλημένος κατέληγε σε γενική σελίδα «Not found» και δεν
            // καταλάβαινε τι πήγε στραβά.
            if (!req.url.includes('account/invite')) {
              router.navigateByUrl('/not-found');
            }
            break;
          case 500:
            //toast.error('Server Error');
            //Περνάμε το error object ως state για να το διαβάσει ο Constructor του component
            const navigationExtras: NavigationExtras = {state: {error: error.error}};
            router.navigateByUrl('/server-error', navigationExtras);
            break;
            default:
              toast.error('Something unexpected went wrong');
              console.log(error);
              break;
        }
      }
        //return throwError(() => error);
        throw error;
    })
     
  )
};