import { Routes } from '@angular/router';
import { Home } from '../features/home/home';
import { RentViewTable } from '../features/rent-view-table/rentt-view-table';

export const routes: Routes = [
    {path:'', component: Home},

    {path:'Rent-view', component:RentViewTable}
];
