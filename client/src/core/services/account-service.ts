import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { LoginDto, MemberInviteDto, MemberInviteInfoDto, MemberRegisterFromInviteDto, PlanType, TenantMemberDto, TenantRegisterDto,  UserDto } from '../../types/user';
import { catchError, tap, throwError } from 'rxjs';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private baseUrl = environment.apiUrl;

  private currentUserSignal = signal<UserDto | null>(this.loadUserFromStorage());
  currentUser = this.currentUserSignal.asReadonly();
  isLoggedIn = computed(() => !!this.currentUserSignal());

  // για πακέτο που έχει αγοράχει
  planType  = computed(() => this.currentUserSignal()?.planType ?? PlanType.Free);
  isBasic   = computed(() => this.planType() >= PlanType.Basic);
  isPro     = computed(() => this.planType() >= PlanType.Pro);

  login(dto: LoginDto) {
    return this.http.post<UserDto>(`${this.baseUrl}account/login`, dto).pipe(
      tap(user => this.setCurrentUser(user))
    );
  }

  register(dto: TenantRegisterDto) {
    console.log(dto);
    return this.http.post<UserDto>(`${this.baseUrl}account`, dto).pipe(
      tap(user => this.setCurrentUser(user))
    );
  }

  
  refreshToken() {
    return this.http.post<UserDto>(`${this.baseUrl}account/refresh-token`, {},  {withCredentials:true}).pipe(
      tap(user => this.setCurrentUser(user)),
      catchError(error => {
        this.logout();
        return throwError(() => error);
      })
    );
  }

  getMembers() {
    return this.http.get<TenantMemberDto[]>(`${this.baseUrl}account/members`);
  }

  setMemberActive(id: string, isActive: boolean) {
    return this.http.patch<{ message: string }>(
      `${this.baseUrl}account/members/${id}/active`, { isActive });
  }

  invite(dto: MemberInviteDto) {
    return this.http.post<{ message: string }>(`${this.baseUrl}account/invite`, dto);
  }

  getInviteInfo(token: string) {
    return this.http.get<MemberInviteInfoDto>(`${this.baseUrl}account/invite`, { params: { token } });
  }

  registerFromInvite(dto: MemberRegisterFromInviteDto) {
    return this.http.post<UserDto>(`${this.baseUrl}account/MemberRegister`, dto).pipe(
      tap(user => this.setCurrentUser(user))
    );
  }


  // redirect=false όταν ο caller θέλει να παραμείνει στη σελίδα του (π.χ. η
  // σελίδα πρόσκλησης, όπου ο χρήστης αποσυνδέεται για να συνεχίσει εκεί).
  logout(redirect: boolean = true) {
    localStorage.removeItem('user');
    this.currentUserSignal.set(null);
    if (redirect) this.router.navigateByUrl('/login');
  }
  
  private setCurrentUser(user: UserDto) {
    user.roles= this.getRolesFromToken(user);
    localStorage.setItem('user', JSON.stringify(user));
    this.currentUserSignal.set(user);
  }

  private loadUserFromStorage(): UserDto | null {
    const userJson = localStorage.getItem('user');
    return userJson ? JSON.parse(userJson) : null;
  }


  private getRolesFromToken(user :UserDto) :string[]{
    const payload = user.token.split(".")[1];
    const decoded=atob(payload);
    const jsonPayload =JSON.parse(decoded);
    return Array.isArray(jsonPayload.role)? jsonPayload.role :[jsonPayload.role];
  }
}