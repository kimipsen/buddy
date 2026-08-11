import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ItemsService, Item } from './items.service';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  standalone: true,
  selector: 'app-items-list',
  imports: [CommonModule],
  templateUrl: './items-list.component.html',
})
export class ItemsListComponent {
  // signals for UI state
  items = signal<Item[]>([]);
  loading = signal(false);
  page = signal(1);
  pageSize = 20;

  constructor(private itemsService: ItemsService) {
    this.load();
  }

  async load() {
    this.loading.set(true);
    try {
      // use Observable -> Signal interop
      const itemsSignal = toSignal(this.itemsService.fetchItems(this.page(), this.pageSize), { initialValue: [] as Item[] });
      this.items.set(itemsSignal());
    } finally {
      this.loading.set(false);
    }
  }

  nextPage() {
    this.page.update(p => p + 1);
    this.load();
  }

  prevPage() {
    this.page.update(p => Math.max(1, p - 1));
    this.load();
  }
}
