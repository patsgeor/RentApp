import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { TenantRegisterDto } from '../../../types/user';
import { CustomerService } from '../../../core/services/customer-service';

const passwordMatchValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const pass = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return pass === confirm ? null : { passwordMismatch: true };
};

@Component({
  selector: 'app-register-tenant',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register-tenant.html',
  styleUrl: './register-tenant.css',
})
export class RegisterTenant {
  
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private customerService = inject(CustomerService);
  private router = inject(Router);

  loading = signal(false);
  errorMessage = signal('');

  form = this.fb.group({
    companyName: ['', [Validators.required, Validators.maxLength(100)]],
    vatNumber: ['', Validators.maxLength(20)],
    contactInfo: ['', Validators.maxLength(500)],
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
    displayName: ['', [Validators.required, Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]],
    phoneNumber: ['', [Validators.required, Validators.maxLength(20)]],
    password: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(100)]],
    confirmPassword: ['', Validators.required]
  }, { validators: passwordMatchValidator });

  get f() { return this.form.controls; }

  onRegister() {
    if (this.form.invalid) return;

    this.loading.set(true);
    this.errorMessage.set('');

    this.accountService.register(this.form.value as TenantRegisterDto).subscribe({
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

    lookupAfm() {
    const vatNumber = this.form.get('vatNumber')?.value;
    if (!vatNumber || vatNumber.length !== 9) return;

    this.customerService.getAadeCompany(vatNumber).subscribe({
      next: (data) => {
        this.errorMessage.set('');

        this.form.patchValue({
          companyName: data.name ?? '',
          // dou:  data.doyDescription ?? '',
          // address: `${data.address ?? ''} ${data.addressNo ?? ''}`.trim(),
        });
      },
      error: () => {
        this.errorMessage.set('Δεν βρέθηκαν στοιχεία ΑΑΔΕ.');
        this.form.patchValue({
          companyName: '',
          // dou:   '',
          // address: '',
        });
      } 
    });
  }

}
