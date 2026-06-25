import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-home-nav',
  imports: [RouterLink,RouterLinkActive],
  templateUrl: './home-nav.html',
  styleUrl: './home-nav.css',
})
export class HomeNav {
    navLinks = [
    { label: 'Χαρακτηριστικά', href: '#features' },
    { label: 'Τιμοκατάλογος', href: '#pricing' },
  ];
}
