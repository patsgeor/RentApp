import { Component, computed, HostListener, inject, OnInit, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterModule, RouterOutlet } from '@angular/router';
import { AccountService } from '../../core/services/account-service';
import { themes } from './themes';

@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './nav.html',
  styleUrl: './nav.css',
})
export class Nav implements OnInit {
  accountService = inject(AccountService);
  sidebarOpen = signal(false);
  isAdmin = computed(() => (this.accountService.currentUser() as any)?.roles?.includes('Admin') ?? false);
  
  protected selectedTheme = signal<string>(localStorage.getItem('theme') || 'dark');
  readonly themes= themes;

  ngOnInit(): void {
    //  Apply the theme to the HTML element immediately on load
    document.documentElement.setAttribute('data-theme', this.selectedTheme());
  }

  handleSelectTheme(theme: string) {
    this.selectedTheme.set(theme);
    localStorage.setItem('theme',theme);
    document.documentElement.setAttribute('data-theme', theme);
  }


  @HostListener('document:keydown.escape')
  onEsc() { this.sidebarOpen.set(false); }

  toggleSidebar() { this.sidebarOpen.update(v => !v); }
  closeSidebar() { this.sidebarOpen.set(false); }
  logout() { this.accountService.logout(); }
}
