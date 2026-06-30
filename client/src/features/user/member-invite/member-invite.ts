import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { MemberInviteDto } from '../../../types/user';

@Component({
  selector: 'app-member-invite',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './member-invite.html',
})
export class MemberInvite {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);

  loading = signal(false);
  errorMessage = signal('');
  successMessage = signal('');

  form = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]],
    role: ['Member', Validators.required],
  });

  get f() { return this.form.controls; }

  onInvite() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.accountService.invite(this.form.value as MemberInviteDto).subscribe({
      next: () => {
        this.successMessage.set('Η πρόσκληση στάλθηκε με επιτυχία.');
        this.form.reset({ role: 'Member' });
        this.loading.set(false);
      },
      error: (err) => {
        const errorObj = err.error;
        if (typeof errorObj === 'string') {
          this.errorMessage.set(errorObj);
        } else if (errorObj?.errors) {
          const messages = Object.values(errorObj.errors).flat().join(' ');
          this.errorMessage.set(messages as string);
        } else if (errorObj?.message) {
          this.errorMessage.set(errorObj.message);
        } else {
          this.errorMessage.set('Σφάλμα κατά την αποστολή της πρόσκλησης.');
        }
        this.loading.set(false);
      }
    });
  }
}