import { Component, signal } from '@angular/core';
import { HomeFooter } from "../home-footer/home-footer";
import { HomeNav } from "../home-nav/home-nav";
import { BillingCycle, CYCLES, DISCOUNTS,PLANS } from  '../../../types/pricingData';
import { Feature, Plan } from '../../../types/generalTypes';

@Component({
  selector: 'app-landing-page',
  imports: [HomeFooter, HomeNav],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.css',
})
export class LandingPage {

  features: Feature[] = [
    {
      icon: 'shield',
      title: 'Πλήρης Απομόνωση Δεδομένων',
      description: 'Multi-tenant αρχιτεκτονική όπου τα δεδομένα κάθε οργανισμού παραμένουν αυστηρά απομονωμένα και ασφαλή.',
    },
    {
      icon: 'puzzle',
      title: 'Δυναμικά EAV Χαρακτηριστικά',
      description: 'Ορίστε custom χαρακτηριστικά για τα πάγιά σας χωρίς αλλαγές στη βάση. Πλήρης ευελιξία ανά κατηγορία.',
    },
    {
      icon: 'key',
      title: 'Εύκολη Ενοικίαση',
      description: 'Διαχειριστείτε τον κύκλο ζωής κάθε ενοικίασης — από την κράτηση έως την επιστροφή — με λίγα κλικ.',
    },
    {
      icon: 'chart',
      title: 'Αναφορές & Insights',
      description: 'Live dashboards και αυτοματοποιημένες αναφορές για να παρακολουθείτε την απόδοση των παγίων σας.',
    },
  ];

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