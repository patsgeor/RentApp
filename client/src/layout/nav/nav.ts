import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterModule, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-nav',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './nav.html',
  styleUrl: './nav.css',
})
export class Nav {}
