// client/src/features/auth/forgot-password/forgot-password.ts
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './forgot-password.html',
})
export class ForgotPassword {
  email    = signal('');
  loading  = signal(false);
  sent     = signal(false);
  error    = signal('');

  constructor(private http: HttpClient) {}

  submit() {
    if (!this.email()) return;
    this.loading.set(true);
    this.error.set('');

    this.http.post(`${environment.apiUrl}account/forgot-password`, { email: this.email() })
      .subscribe({
        next: () => { this.sent.set(true); this.loading.set(false); },
        error: () => { this.error.set('Κάτι πήγε στραβά. Δοκίμασε ξανά.'); this.loading.set(false); }
      });
  }
}