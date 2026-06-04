import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private http =inject(HttpClient)
  private currentUser=signal<null>(null)
}
