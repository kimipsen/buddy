import { Component, OnInit, inject, signal } from '@angular/core';

import { ChildSummary, GuardiansService } from '../../../core/guardians.service';

@Component({
  selector: 'app-children-overview',
  templateUrl: './children-overview.html'
})
export class ChildrenOverview implements OnInit {
  private readonly guardians = inject(GuardiansService);

  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadChildren();
  }

  private async loadChildren(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.children.set(await this.guardians.listMyChildren());
    } catch {
      this.error.set('Unable to load children.');
    } finally {
      this.loading.set(false);
    }
  }
}
