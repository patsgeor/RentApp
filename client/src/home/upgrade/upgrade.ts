import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Plan } from '../../types/generalTypes';
import { BillingCycle, CYCLES, DISCOUNTS, PLANS } from '../../types/pricingData';
import { AccountService } from '../../core/services/account-service';
import { UpgradeRequestService } from '../../core/services/upgrade-request-service';
import { PlanType, UpgradeRequestStatus } from '../../types/user';

// Το marketing page διαφημίζει και "Enterprise", που δεν υπάρχει ακόμα σαν
// πραγματικό PlanType στο backend — γι' αυτό εκείνη η κάρτα παραμένει πάντα
// σε "Επικοινωνήστε" (custom πωλήσεις), όχι σε αυτόματο αίτημα.
const PLAN_TYPE_BY_NAME: Record<string, PlanType> = { Basic: PlanType.Basic, Pro: PlanType.Pro };

@Component({
  selector: 'app-upgrade',
  imports: [RouterLink],
  templateUrl: './upgrade.html',
})
export class Upgrade implements OnInit {
  private accountService = inject(AccountService);
  private upgradeService = inject(UpgradeRequestService);

  billing = signal<BillingCycle>('monthly');
  cycles = CYCLES;
  plans = PLANS;

  isLoggedIn   = this.accountService.isLoggedIn;
  currentPlan  = this.accountService.planType;
  requestingId = signal<string | null>(null);
  requestError = signal('');
  pendingPlan  = signal<PlanType | null>(null); // ζητούμενο πλάνο εκκρεμούς αιτήματος, αν υπάρχει

  ngOnInit() {
    if (!this.isLoggedIn()) return;
    this.upgradeService.getMine().subscribe({
      next: rows => {
        const pending = rows.find(r => r.status === UpgradeRequestStatus.Pending);
        this.pendingPlan.set(pending ? pending.requestedPlanType : null);
      }
    });
  }

  priceFor(plan: Plan): number {
    return Math.round(plan.monthly * (1 - DISCOUNTS[this.billing()]));
  }

  selectCycle(cycle: BillingCycle): void {
    this.billing.set(cycle);
  }

  // Πλάνα χωρίς αντιστοιχία (Enterprise) ή ίσα/κατώτερα από το τρέχον δεν
  // προσφέρουν αυτόματο αίτημα — μόνο Basic/Pro πάνω από το τρέχον πλάνο.
  planTypeFor(plan: Plan): PlanType | null {
    return PLAN_TYPE_BY_NAME[plan.name] ?? null;
  }

  canRequest(plan: Plan): boolean {
    const pt = this.planTypeFor(plan);
    return pt !== null && pt > this.currentPlan();
  }

  requestUpgrade(plan: Plan) {
    const pt = this.planTypeFor(plan);
    if (pt === null) return;

    this.requestError.set('');
    this.requestingId.set(plan.name);
    this.upgradeService.request(pt).subscribe({
      next: () => { this.pendingPlan.set(pt); this.requestingId.set(null); },
      error: err => {
        this.requestError.set(err.error?.message ?? 'Σφάλμα υποβολής αιτήματος.');
        this.requestingId.set(null);
      }
    });
  }
}