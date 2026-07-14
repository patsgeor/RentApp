import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './change-password.html',
})
export class ChangePassword {
  current      = signal('');
  newPass      = signal('');
  confirm      = signal('');
  loading      = signal(false);
  success      = signal(false);
  serverErrors = signal<string[]>([]);

  passwordErrors = computed(() => {
    const pwd = this.newPass();
    if (!pwd) return [];
    const errs: string[] = [];
    if (pwd.length < 6)       errs.push('Τουλάχιστον 6 χαρακτήρες.');
    if (!/[A-Z]/.test(pwd))   errs.push('Τουλάχιστον ένα κεφαλαίο γράμμα.');
    if (!/[a-z]/.test(pwd))   errs.push('Τουλάχιστον ένα πεζό γράμμα.');
    if (!/[0-9]/.test(pwd))   errs.push('Τουλάχιστον έναν αριθμό.');
    return errs;
  });

  confirmError = computed(() =>
    this.confirm() && this.newPass() !== this.confirm()
      ? 'Οι κωδικοί δεν ταιριάζουν.'
      : ''
  );

  isValid = computed(() =>
    !!this.current() &&
    this.passwordErrors().length === 0 &&
    !this.confirmError() &&
    !!this.newPass() &&
    !!this.confirm()
  );

  constructor(private http: HttpClient) {}

  submit() {
    if (!this.isValid()) return;
    this.loading.set(true);
    this.serverErrors.set([]);

    this.http.post(`${environment.apiUrl}account/change-password`, {
      currentPassword: this.current(),
      newPassword:     this.newPass(),
      confirmPassword: this.confirm(),
    }).subscribe({
      next: () => {
        this.success.set(true);
        this.loading.set(false);
        this.current.set('');
        this.newPass.set('');
        this.confirm.set('');
      },
      error: (err) => {
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