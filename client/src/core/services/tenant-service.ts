import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { TenantAdminDto, SubscriptionStatus } from '../../types/tenant';
import { PlanType } from '../../types/user';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}tenant`;

  getAll() {
    return this.http.get<TenantAdminDto[]>(this.base);
  }

  updatePlan(id: string, planType: PlanType) {
    return this.http.patch<{ id: string; name: string; planType: PlanType }>(
      `${this.base}/${id}/plan`, { planType }
    );
  }

  updateStatus(id: string, subscriptionStatus: SubscriptionStatus) {
    return this.http.patch<{ id: string; name: string; subscriptionStatus: SubscriptionStatus }>(
      `${this.base}/${id}/status`, { subscriptionStatus }
    );
  }
}
