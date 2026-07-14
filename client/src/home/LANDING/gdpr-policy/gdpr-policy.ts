import { Component } from '@angular/core';
import { HomeNav } from '../home-nav/home-nav';
import { HomeFooter } from '../home-footer/home-footer';

@Component({
  selector: 'app-gdpr-policy',
  standalone: true,
  imports: [HomeNav, HomeFooter],
  templateUrl: './gdpr-policy.html',
})
export class GdprPolicy {
  lastUpdated = '14 Ιουλίου 2025';
}