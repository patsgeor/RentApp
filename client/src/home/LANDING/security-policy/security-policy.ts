import { Component } from '@angular/core';
import { HomeNav } from '../home-nav/home-nav';
import { HomeFooter } from '../home-footer/home-footer';

@Component({
  selector: 'app-security-policy',
  standalone: true,
  imports: [HomeNav, HomeFooter],
  templateUrl: './security-policy.html',
})
export class SecurityPolicy {
  lastUpdated = '14 Ιουλίου 2025';
}