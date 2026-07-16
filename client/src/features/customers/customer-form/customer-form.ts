import { Component, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CustomerService } from '../../../core/services/customer-service';
import { ContactDto } from '../../../types/customers';


@Component({
  selector: 'app-customer-form',
  imports: [ReactiveFormsModule,RouterLink],
  templateUrl: './customer-form.html',
  styleUrl: './customer-form.css',
})
export class CustomerForm implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(CustomerService);
  private customerRowVersion = 0;
  private contactRowVersion  = 0;

  // ── Customer form ──────────────────────────────────────────────────────
  isEdit   = signal(false);
  loading  = signal(false);
  errorMsg = signal('');
  private customerId: string | null = null;

  form = this.fb.group({
    name:    ['', Validators.required],
    afm:     ['', [Validators.required, Validators.pattern(/^\d{9}$/)]],
    dou:      [''],
    address: [''],
    representative: [''],
  });

  get f() { return this.form.controls; }

  // ── Contacts ───────────────────────────────────────────────────────────
  contacts         = signal<ContactDto[]>([]);
  editingContactId = signal<string | null>(null);
  showAddContact   = signal(false);
  contactSaving    = signal(false);
  contactError     = signal('');

  contactForm = this.fb.group({
    name:   ['', Validators.required],
    phone:       [''],
    email:       ['', Validators.email],
    canUseAsset: [false],
    notes:       [''],
  });

  get cf() { return this.contactForm.controls; }

  // ── Lifecycle ──────────────────────────────────────────────────────────
  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.customerId = id;
     this.service.getById(id).subscribe(c => {
        this.customerRowVersion = c.rowVersion ?? 0;   // ← ADD
        this.form.patchValue({
          name: c.name, afm: c.afm,
          dou: c.dou ?? '', address: c.address ?? '', representative: c.representative ?? '',
        });
        this.contacts.set(c.contacts ?? []);
      });
    }

      this.form.get('afm')?.valueChanges.subscribe(value => {
      if (value?.length === 9) {
        this.lookupAfm();
      }
    });
  }

  // ── Customer submit ────────────────────────────────────────────────────
  onSubmit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.errorMsg.set('');
    const dto = this.form.value as any;
    const req = this.isEdit()
      ? this.service.update(this.customerId!, { ...dto, rowVersion: this.customerRowVersion })  // ← ADD rowVersion
      : this.service.create(dto);
    req.subscribe({
      next: (saved) => {
        if (!this.isEdit()) {
          // After create, go to edit so contacts can be added
          this.router.navigate(['/customer', saved.id, 'edit']);
        } else {
          this.router.navigateByUrl('/customer');
        }
      },
      error: (err: any) => {
        this.errorMsg.set(err.error?.message || 'Σφάλμα αποθήκευσης.');
        this.loading.set(false);
      }
    });

  }

  // ── Contact actions ────────────────────────────────────────────────────
  startAddContact() {
    this.editingContactId.set(null);
    this.contactError.set('');
    this.contactForm.reset({ canUseAsset: false });
    this.showAddContact.set(true);
  }

  startEditContact(c: ContactDto) {
    this.showAddContact.set(false);
    this.contactError.set('');
    this.contactRowVersion = c.rowVersion ?? 0;   // ← ADD
    this.contactForm.patchValue({
      name: c.name, 
      phone: c.phone ?? '', email: c.email ?? '',
      canUseAsset: c.canUseAsset, notes: c.notes ?? '',
    });
    this.editingContactId.set(c.id);
  }

  cancelContact() {
    this.editingContactId.set(null);
    this.showAddContact.set(false);
    this.contactError.set('');
  }

  saveContact() {
    if (this.contactForm.invalid) { this.contactForm.markAllAsTouched(); return; }
    this.contactSaving.set(true);
    this.contactError.set('');
    const dto = this.contactForm.value;
    const editId = this.editingContactId();

    if (editId) {
      this.service.updateContact(this.customerId!, editId, { ...dto, rowVersion: this.contactRowVersion }).subscribe({  
        next: (updated) => {
          this.contacts.update(list => list.map(c => c.id === editId ? updated : c));
          this.editingContactId.set(null);
          this.contactSaving.set(false);
        },
        error: () => { this.contactError.set('Σφάλμα ενημέρωσης επαφής.'); this.contactSaving.set(false); }
      });
    } else {
      this.service.addContact(this.customerId!, dto).subscribe({
        next: (created) => {
          this.contacts.update(list => [...list, created]);
          this.showAddContact.set(false);
          this.contactSaving.set(false);
        },
        error: () => { this.contactError.set('Σφάλμα προσθήκης επαφής.'); this.contactSaving.set(false); }
      });
    }
  }

  deleteContact(id: string) {
    if (!confirm('Διαγραφή επαφής;')) return;
    this.service.deleteContact(this.customerId!, id).subscribe({
      next: () => this.contacts.update(list => list.filter(c => c.id !== id))
    });
  }

  

  lookupAfm() {
    const afm = this.form.get('afm')?.value;
    if (!afm || afm.length !== 9) return;

    this.service.getAadeCompany(afm).subscribe({
      next: (data) => {
        this.errorMsg.set('');

        this.form.patchValue({
          name: data.name ?? '',
          dou:  data.doyDescription ?? '',
          address: `${data.address ?? ''} ${data.addressNo ?? ''}`.trim(),
        });
      },
      error: () => {
        // this.errorMsg.set('Δεν βρέθηκαν στοιχεία ΑΑΔΕ.');
        this.form.patchValue({
          name: '',
          dou:   '',
          address: '',
        });
      } 
    });
  }
}
