import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HomeNav } from '../home-nav/home-nav';
import { HomeFooter } from '../home-footer/home-footer';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [HomeNav, HomeFooter, FormsModule],
  templateUrl: './contact.html',
})
export class Contact {
  name    = signal('');
  email   = signal('');
  subject = signal('');
  message = signal('');
  loading = signal(false);
  success = signal(false);
  error   = signal('');

  constructor(private http: HttpClient) {}

  isValid() {
    return this.name() && this.email() && this.subject() && this.message().length >= 10;
  }

  submit() {
    if (!this.isValid()) return;
    this.loading.set(true);
    this.error.set('');

    this.http.post(`${environment.apiUrl}emailContact`, {
      name:    this.name(),
      email:   this.email(),
      subject: this.subject(),
      message: this.message(),
    }).subscribe({
      next: () => { this.success.set(true); this.loading.set(false); },
      error: () => { this.error.set('Κάτι πήγε στραβά. Δοκιμάστε ξανά ή επικοινωνήστε απευθείας.'); this.loading.set(false); }
    });
  }
}