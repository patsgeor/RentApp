import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { PlanType, UpgradeRequestDto } from '../../types/user';

@Injectable({ providedIn: 'root' })
export class UpgradeRequestService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}UpgradeRequest`;

  getMine() {
    return this.http.get<UpgradeRequestDto[]>(this.base);
  }

  request(requestedPlanType: PlanType, note?: string) {
    return this.http.post<UpgradeRequestDto>(this.base, { requestedPlanType, note });
  }
}
