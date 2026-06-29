import { Component, inject, OnInit } from '@angular/core';
import { AccountService } from '../../core/services/account-service';

@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  accountService=inject(AccountService);
  

}
