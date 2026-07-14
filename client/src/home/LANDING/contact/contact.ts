import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AccountService } from '../../../core/services/account-service';

@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [ FormsModule],
  templateUrl: './contact.html',
})
export class Contact {
  private http = inject(HttpClient);
  accountService = inject(AccountService);

  name    = signal('');
  email   = signal('');
  message = signal('');
  subject = signal('');
  loading = signal(false);
  success = signal(false);
  error = signal('');

  ngOnInit() {
    const user = this.accountService.currentUser();
    if (user) {
      this.name.set(user.displayName ?? '');
      this.email.set(user.email ?? '');
    }
  }

   isValid() {
    return this.name() && this.email() && this.subject() && this.message().length >= 10;
  }

  submit() {
    if (!this.name() || !this.email() || !this.message()) return;
    this.loading.set(true);
    this.error.set('');
    this.http.post(`${environment.apiUrl}contact`, {
      name: this.name(),
      email: this.email(),
      message: this.message(),
    }).subscribe({
      next: () => {
        this.success.set(true);
        this.loading.set(false);
        this.message.set('');
      },
      error: () => {
        this.error.set('Κάτι πήγε στραβά. Δοκιμάστε ξανά.');
        this.loading.set(false);
      }
    });
  }
}