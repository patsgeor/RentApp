import { Component, signal } from '@angular/core';
import { Feature, Plan } from '../../types/generalTypes';
import { HomeFooter } from "../home-footer/home-footer";
import { HomeNav } from "../home-nav/home-nav";

type BillingCycle = 'monthly' | 'yearly' | 'fiveYear';


@Component({
  selector: 'app-landing-page',
  imports: [HomeFooter, HomeNav],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.css',
})
export class LandingPage {

  // -- Why choose us --
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

  // -- Pricing --
  billing = signal<BillingCycle>('monthly');

  private discounts: Record<BillingCycle, number> = {
    monthly: 0,
    yearly: 0.10,
    fiveYear: 0.15,
  };

  cycles = [
    { id: 'monthly' as BillingCycle, label: 'Μηνιαία' },
    { id: 'yearly' as BillingCycle, label: 'Ετήσια (-10%)' },
    { id: 'fiveYear' as BillingCycle, label: '5ετής (-15%)' },
  ];

  plans: Plan[] = [
    {
      name: 'Basic',
      tagline: 'Για μικρές ομάδες που ξεκινούν.',
      monthly: 29,
      features: ['Απλή καταχώρηση παγίων', 'Αναζήτηση & φίλτρα', 'Βασικό dashboard'],
      highlighted: false,
      cta: 'Ξεκινήστε',
    },
    {
      name: 'Pro',
      tagline: 'Για επιχειρήσεις σε ανάπτυξη.',
      monthly: 79,
      features: ['Όλα του Basic', 'Barcode / QR scanner', 'Email ειδοποιήσεις', 'Προηγμένες αναφορές'],
      highlighted: true,
      cta: 'Δοκιμάστε το Pro',
    },
    {
      name: 'Enterprise',
      tagline: 'Για custom, μεγάλης κλίμακας ανάγκες.',
      monthly: 199,
      features: ['Όλα του Pro', 'API access', 'Auto-reports', 'Custom integrations', 'Dedicated support'],
      highlighted: false,
      cta: 'Επικοινωνήστε',
    },
  ];

  // Computed τιμή ανά πλάνο βάσει του επιλεγμένου κύκλου
  priceFor(plan: Plan): number {
    const discount = this.discounts[this.billing()];
    return Math.round(plan.monthly * (1 - discount));
  }

  selectCycle(cycle: BillingCycle): void {
    this.billing.set(cycle);
  }
}