import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ToastService {


  constructor() {
    // Μόλις δημιουργηθεί το service, φτιάχνουμε το toast container μία φορά
    this.createToastContainer();
  }

  // Δημιουργεί το container που θα κρατάει ΟΛΑ τα toast messages.
  // Εκτελείται μόνο μία φορά (αν δεν υπάρχει ήδη).
  private createToastContainer() {
    if (!document.getElementById('toast-container')) {
      const toast = document.createElement('div');
      toast.id = 'toast-container';
      toast.className = 'toast toast-bottom toast-end z-50';
      document.body.append(toast);
    }
  }

   // Δημιουργεί ΕΝΑ toast message.
  private createToastElement(message: string, alertClass: string, duration = 5000) {
    const toastContainer = document.getElementById('toast-container');
    if (!toastContainer) return;
    const toast = document.createElement('div');
    toast.classList.add('alert', alertClass, 'shadow-lg','items-center', 'gap-3', 'cursor-pointer',
      'opacity-100',
      'transition-opacity',
      'duration-500');

    // HTML περιεχόμενο: μήνυμα + κουμπί κλεισίματος
    toast.innerHTML = `
    <span>${message}</span>
    <button class="btn btn-sm btn-ghost ml-4 " type="button" > x </button>
    `;

    toast.querySelector('button')?.addEventListener('click', () => {
      toastContainer.removeChild(toast);// ή toast.remove();
    });

    // Προσθήκη του toast στο container
    toastContainer.append(toast);

    setTimeout(() => {
      if (toastContainer.contains(toast)) {
        // toastContainer.removeChild(toast);
        toast.style.opacity = '0';
        setTimeout(() => toast.remove(), 500);
      }
    }, duration);
  }

  success(message: string, duration?: number) {
    this.createToastElement(message, 'alert-success', duration);
  }

  info(message: string, duration?: number) {
    this.createToastElement(message, 'alert-info');
  }

  error(message: string, duration?: number, avatar?: string, route?: string) {
    this.createToastElement(message, 'alert-error');
  }

  warning(message: string, duration?: number, avatar?: string) {
    this.createToastElement(message, 'alert-warning');
  }

}
