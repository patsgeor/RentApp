import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HomeNav } from '../LANDING/home-nav/home-nav';
import { HomeFooter } from '../LANDING/home-footer/home-footer';

@Component({
  selector: 'app-public-layout',
  imports: [RouterOutlet, HomeNav, HomeFooter],
  templateUrl: './public-layout.html',
})
export class PublicLayout {}