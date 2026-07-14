import { Plan } from "./generalTypes";

export type BillingCycle = 'monthly' | 'yearly' | 'fiveYear';

export const CYCLES = [
  { id: 'monthly' as BillingCycle, label: 'Μηνιαία' },
  { id: 'yearly' as BillingCycle, label: 'Ετήσια (-10%)' },
  { id: 'fiveYear' as BillingCycle, label: '5ετής (-15%)' },
];

export const DISCOUNTS: Record<BillingCycle, number> = {
  monthly: 0,
  yearly: 0.10,
  fiveYear: 0.15,
};

export const PLANS: Plan[] = [
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