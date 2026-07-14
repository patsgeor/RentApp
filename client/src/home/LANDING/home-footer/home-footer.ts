import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home-footer',
  imports: [RouterLink],
  templateUrl: './home-footer.html',
  styleUrl: './home-footer.css',
})
export class HomeFooter {
  currentYear = new Date().getFullYear();
}
