import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { LoginDto, MemberInviteDto, MemberInviteInfoDto, MemberRegisterFromInviteDto, PlanType, TenantMemberDto, TenantRegisterDto,  UserDto } from '../../types/user';
import { catchError, of, tap, throwError } from 'rxjs';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private static readonly STORAGE_KEY = 'user';

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

  // withCredentials σε κάθε κλήση που δημιουργεί, ανανεώνει ή σβήνει το cookie
  // ανανέωσης: χωρίς αυτό ο browser αγνοεί το Set-Cookie μιας απόκρισης από άλλη
  // προέλευση — και σε παραγωγή το API βρίσκεται σε διαφορετικό domain από το
  // frontend, οπότε η συνεδρία δεν θα μπορούσε ποτέ να ανανεωθεί.
  login(dto: LoginDto) {
    return this.http.post<UserDto>(`${this.baseUrl}account/login`, dto, { withCredentials: true }).pipe(
      tap(user => this.setCurrentUser(user))
    );
  }

  register(dto: TenantRegisterDto) {
    return this.http.post<UserDto>(`${this.baseUrl}account`, dto, { withCredentials: true }).pipe(
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

  /**
   * Επαναφορά συνεδρίας στο άνοιγμα της εφαρμογής. Η κατάσταση του χρήστη ζει στο
   * sessionStorage, άρα χάνεται με το κλείσιμο του παραθύρου· όσο όμως ο browser
   * παραμένει ανοιχτός, το cookie ανανέωσης επιτρέπει σιωπηλή επανασύνδεση σε νέα
   * καρτέλα. Η αποτυχία αγνοείται: ο επισκέπτης απλώς παραμένει αποσυνδεδεμένος.
   */
  restoreSession() {
    if (this.currentUserSignal()) return of(null);

    return this.http.post<UserDto>(`${this.baseUrl}account/refresh-token`, {}, { withCredentials: true }).pipe(
      tap(user => { if (user) this.setCurrentUser(user); }),
      catchError(() => of(null))
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
    return this.http.post<UserDto>(`${this.baseUrl}account/MemberRegister`, dto, { withCredentials: true }).pipe(
      tap(user => this.setCurrentUser(user))
    );
  }


  // redirect=false όταν ο caller θέλει να παραμείνει στη σελίδα του (π.χ. η
  // σελίδα πρόσκλησης, όπου ο χρήστης αποσυνδέεται για να συνεχίσει εκεί).
  logout(redirect: boolean = true) {
    // Ο server ακυρώνει το refresh token στη βάση και σβήνει το cookie. Χωρίς αυτό,
    // το καθάρισμα του browser άφηνε τη συνεδρία ζωντανή για επτά ημέρες.
    // Η απάντηση δεν μας ενδιαφέρει: η αποσύνδεση στο UI γίνεται ούτως ή άλλως.
    this.http.post(`${this.baseUrl}account/logout`, {}, { withCredentials: true })
      .subscribe({ error: () => { } });

    this.clearSession();
    if (redirect) this.router.navigateByUrl('/login');
  }

  private clearSession() {
    sessionStorage.removeItem(AccountService.STORAGE_KEY);
    this.currentUserSignal.set(null);
  }

  private setCurrentUser(user: UserDto) {
    user.roles= this.getRolesFromToken(user);
    sessionStorage.setItem(AccountService.STORAGE_KEY, JSON.stringify(user));
    this.currentUserSignal.set(user);
  }

  // sessionStorage και όχι localStorage: το κλείσιμο του παραθύρου τερματίζει τη
  // συνεδρία, όπως και το cookie ανανέωσης που είναι cookie συνεδρίας. Σε
  // κοινόχρηστο υπολογιστή, ο επόμενος χρήστης δεν βρίσκει ανοιχτή τη δουλειά μας.
  private loadUserFromStorage(): UserDto | null {
    // Μεταβατικά: παλιές συνεδρίες έμεναν στο localStorage και θα επιβίωναν για πάντα.
    localStorage.removeItem(AccountService.STORAGE_KEY);

    const userJson = sessionStorage.getItem(AccountService.STORAGE_KEY);
    return userJson ? JSON.parse(userJson) : null;
  }


  private getRolesFromToken(user :UserDto) :string[]{
    const payload = user.token.split(".")[1];
    const decoded=atob(payload);
    const jsonPayload =JSON.parse(decoded);
    return Array.isArray(jsonPayload.role)? jsonPayload.role :[jsonPayload.role];
  }
}