import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccountService } from '../../../core/services/account-service';
import { TenantMemberDto } from '../../../types/user';

@Component({
  selector: 'app-member-list',
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './member-list.html',
})
export class MemberList implements OnInit {
  private accountService = inject(AccountService);

  members  = signal<TenantMemberDto[]>([]);
  loading  = signal(false);
  savingId = signal<string | null>(null);
  errorMsg = signal('');
  successMsg = signal('');
  search   = signal('');

  /** Ο ίδιος ο συνδεδεμένος διαχειριστής — δεν επιτρέπεται να αυτοαπενεργοποιηθεί. */
  private currentUserId = computed(() => this.accountService.currentUser()?.id ?? '');

  filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const list = this.members();
    if (!term) return list;
    return list.filter(m =>
      `${m.firstName} ${m.lastName}`.toLowerCase().includes(term) ||
      m.displayName.toLowerCase().includes(term) ||
      m.email.toLowerCase().includes(term));
  });

  activeCount   = computed(() => this.members().filter(m => m.isActive).length);
  inactiveCount = computed(() => this.members().filter(m => !m.isActive).length);

  isSelf(m: TenantMemberDto) { return m.id === this.currentUserId(); }

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.errorMsg.set('');
    this.accountService.getMembers().subscribe({
      next: list => { this.members.set(list); this.loading.set(false); },
      error: () => {
        this.errorMsg.set('Σφάλμα φόρτωσης χρηστών.');
        this.loading.set(false);
      }
    });
  }

  toggleActive(m: TenantMemberDto) {
    if (this.isSelf(m)) return;

    const next = !m.isActive;
    const verb = next ? 'ενεργοποιήσετε' : 'απενεργοποιήσετε';
    const name = `${m.firstName} ${m.lastName}`.trim() || m.displayName;

    if (!confirm(`Θέλετε σίγουρα να ${verb} τον χρήστη ${name};`)) return;

    this.savingId.set(m.id);
    this.errorMsg.set('');
    this.successMsg.set('');

    this.accountService.setMemberActive(m.id, next).subscribe({
      next: res => {
        // Ενημέρωση τοπικά αντί για επαναφόρτωση όλης της λίστας.
        this.members.update(list =>
          list.map(x => x.id === m.id ? { ...x, isActive: next } : x));
        this.successMsg.set(res.message);
        this.savingId.set(null);
      },
      error: err => {
        this.errorMsg.set(typeof err.error === 'string'
          ? err.error
          : 'Δεν ήταν δυνατή η αλλαγή κατάστασης του χρήστη.');
        this.savingId.set(null);
      }
    });
  }
}
