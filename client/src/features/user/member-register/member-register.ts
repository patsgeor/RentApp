import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { MemberInviteInfoDto, MemberRegisterFromInviteDto } from '../../../types/user';

const passwordMatchValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const pass = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return pass === confirm ? null : { passwordMismatch: true };
};

@Component({
  selector: 'app-member-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './member-register.html',
})
export class MemberRegister implements OnInit {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private token: string | null = null;

  loadingInvite = signal(true);
  inviteInfo = signal<MemberInviteInfoDto | null>(null);
  inviteError = signal('');

  loading = signal(false);
  errorMessage = signal('');

  form = this.fb.group({
    displayName: ['', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(100), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/)]],
    confirmPassword: ['', Validators.required],
  }, { validators: passwordMatchValidator });

  get f() { return this.form.controls; }

  ngOnInit() {
    this.token = this.route.snapshot.queryParamMap.get('token');
    if (!this.token) {
      this.inviteError.set('Μη έγκυρος σύνδεσμος πρόσκλησης.');
      this.loadingInvite.set(false);
      return;
    }
    this.accountService.getInviteInfo(this.token).subscribe({
      next: (info) => {
        this.inviteInfo.set(info);
        this.loadingInvite.set(false);
      },
      error: () => {
        this.inviteError.set('Η πρόσκληση δεν είναι έγκυρη ή έχει λήξει.');
        this.loadingInvite.set(false);
      }
    });
  }

  onRegister() {
    if (this.form.invalid || !this.token) { this.form.markAllAsTouched(); return; }

    this.loading.set(true);
    this.errorMessage.set('');

    const dto: MemberRegisterFromInviteDto = {
      token: this.token,
      displayName: this.form.value.displayName!,
      password: this.form.value.password!,
      confirmPassword: this.form.value.confirmPassword!,
    };

    this.accountService.registerFromInvite(dto).subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: (err) => {
        const errorObj = err.error;
        if (typeof errorObj === 'string') {
          this.errorMessage.set(errorObj);
        } else if (errorObj?.errors) {
          const messages = Object.values(errorObj.errors).flat().join(' ');
          this.errorMessage.set(messages as string);
        } else {
          this.errorMessage.set('Σφάλμα εγγραφής. Παρακαλώ ελέγξτε τα στοιχεία σας.');
        }
        this.loading.set(false);
      }
    });
  }
}
