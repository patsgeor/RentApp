import { Component } from '@angular/core';
import { HomeNav } from '../home-nav/home-nav';
import { HomeFooter } from '../home-footer/home-footer';

@Component({
  selector: 'app-data-backup-policy',
  standalone: true,
  imports: [HomeNav, HomeFooter],
  templateUrl: './data-backup-policy.html',
})
export class DataBackupPolicy {
  lastUpdated = '14 Ιουλίου 2025';
}