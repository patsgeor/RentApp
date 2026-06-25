import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class RentService {
  private http= inject(HttpClient);
  private baseUrl =environment.apiUrl;

  // προβολή παγίων
  getRent(){
    return this.http.get<[]>("https://localhost:5001/api/Rent");
  }
  
  
}
