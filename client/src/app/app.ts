import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from "@angular/router";
import { AccountService } from '../core/services/account-service';

@Component({
  selector: 'app-root',
  imports: [ RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {

  protected readonly title = signal('Rent-ERP');
  protected  accountService=inject(AccountService);
  
 

}
