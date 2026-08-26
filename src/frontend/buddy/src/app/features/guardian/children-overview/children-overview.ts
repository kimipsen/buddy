import { Component, OnInit, inject, signal } from '@angular/core';

import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

@Component({
  selector: 'app-children-overview',
  imports: [TranslatePipe, LoadingSpinner],
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
      this.error.set('dashboard.children.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
