import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterModule, RouterOutlet } from '@angular/router';
import { AccountService } from '../../core/services/account-service';

@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './nav.html',
  styleUrl: './nav.css',
})
export class Nav {
   accountService = inject(AccountService);

   logout(){
    this.accountService.logout();
   }
}
