import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Plan } from '../../types/generalTypes';
import { BillingCycle, CYCLES, DISCOUNTS, PLANS } from '../../types/pricingData';

@Component({
  selector: 'app-upgrade',
  imports: [RouterLink],
  templateUrl: './upgrade.html',
})
export class Upgrade {
  billing = signal<BillingCycle>('monthly');
  cycles = CYCLES;
  plans = PLANS;

  priceFor(plan: Plan): number {
    return Math.round(plan.monthly * (1 - DISCOUNTS[this.billing()]));
  }

  selectCycle(cycle: BillingCycle): void {
    this.billing.set(cycle);
  }
}