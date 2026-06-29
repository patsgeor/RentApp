import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { LoginDto, TenantRegisterDto,  UserDto } from '../../types/user';
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

  login(dto: LoginDto) {
    return this.http.post<UserDto>(`${this.baseUrl}account/login`, dto).pipe(
      tap(user => this.setCurrentUser(user))
    );
  }

  register(dto: TenantRegisterDto) {
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

  logout() {
    localStorage.removeItem('user');
    this.currentUserSignal.set(null);
    this.router.navigateByUrl('/login');
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