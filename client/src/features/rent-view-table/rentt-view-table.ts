import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RentViewDto } from '../../types/Rent';
import { DatePipe } from '@angular/common';
import { RentService } from '../../core/services/rent-service';

@Component({
  selector: 'app-Rent-view-table',
  imports: [DatePipe],
  templateUrl: './Rent-view-table.html',
  styleUrl: './Rent-view-table.css',
})
export class RentViewTable implements OnInit {
  private RentService=inject(RentService);
  Rents=signal<RentViewDto[] | null>(null);
  
  ngOnInit(): void {
    this.loadData();
  }

  loadData():void{
    this.RentService.getRent().subscribe({
      next: data => this.Rents.set(data)
    })
  }
}
