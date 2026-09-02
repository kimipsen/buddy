import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Item {
  id: string;
  title: string;
  description?: string;
}

@Injectable({ providedIn: 'root' })
export class ItemsService {
  constructor(private readonly http: HttpClient) {}

  fetchItems(page = 1, pageSize = 20): Observable<Item[]> {
    return this.http.get<Item[]>(`/api/items?page=${page}&pageSize=${pageSize}`);
  }
}
