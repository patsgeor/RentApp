import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AccountService } from '../../../core/services/account-service';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private router = inject(Router);

  loading = signal(false);
  errorMessage = signal('');

  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  onLogin() {
    if (this.form.invalid) return;

    this.loading.set(true);
    this.errorMessage.set('');

    this.accountService.login(this.form.value as { email: string; password: string }).subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: (err) => {
        this.errorMessage.set(err.error || 'Σφάλμα σύνδεσης. Ελέγξτε τα στοιχεία σας.');
        this.loading.set(false);
      }
    });
  }

  get f() { return this.form.controls; }
}
