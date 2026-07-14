import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './reset-password.html',
})
export class ResetPassword implements OnInit {
  email       = '';
  token       = '';
  newPassword = signal('');
  confirm     = signal('');
  loading     = signal(false);
  success     = signal(false);
  serverErrors = signal<string[]>([]);

  // Client-side validation — ίδιοι κανόνες με Identity
  passwordErrors = computed(() => {
    const pwd = this.newPassword();
    if (!pwd) return [];
    const errs: string[] = [];
    if (pwd.length < 6)          errs.push('Τουλάχιστον 6 χαρακτήρες.');
    if (!/[A-Z]/.test(pwd))      errs.push('Τουλάχιστον ένα κεφαλαίο γράμμα.');
    if (!/[a-z]/.test(pwd))      errs.push('Τουλάχιστον ένα πεζό γράμμα.');
    if (!/[0-9]/.test(pwd))      errs.push('Τουλάχιστον έναν αριθμό.');
    return errs;
  });

  confirmError = computed(() =>
    this.confirm() && this.newPassword() !== this.confirm()
      ? 'Οι κωδικοί δεν ταιριάζουν.'
      : ''
  );

  isValid = computed(() =>
    this.passwordErrors().length === 0 &&
    !this.confirmError() &&
    !!this.newPassword() &&
    !!this.confirm()
  );

  constructor(private route: ActivatedRoute, private http: HttpClient) {}

  ngOnInit() {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.email || !this.token)
      this.serverErrors.set(['Μη έγκυρος σύνδεσμος επαναφοράς.']);
  }

  submit() {
    if (!this.isValid()) return;
    this.loading.set(true);
    this.serverErrors.set([]);

    this.http.post(`${environment.apiUrl}account/reset-password`, {
      email:           this.email,
      token:           this.token,
      newPassword:     this.newPassword(),
      confirmPassword: this.confirm(),
    }).subscribe({
      next: () => { this.success.set(true); this.loading.set(false); },
      error: (err) => {
      console.log(err);
      console.log(err.error);

      const body = err.error;
      
      const errs =
        Array.isArray(err)
          ? err
          : Array.isArray(err.error)
            ? err.error
            : Array.isArray(err.error?.errors)
              ? err.error.errors
              : err.error?.message
                ? [err.error.message]
                : ['Κάτι πήγε στραβά.'];

      this.serverErrors.set(errs);
      this.loading.set(false);
    }
    });
  }
}