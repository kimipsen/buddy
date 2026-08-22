import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../core/auth.service';
import { ChildSummary, CreateChildResult, GuardiansService } from '../../core/guardians.service';
import { EventsList } from './events-list/events-list';

@Component({
  selector: 'app-guardian-dashboard',
  imports: [EventsList, FormsModule],
  templateUrl: './dashboard.html'
})
export class GuardianDashboard implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly guardians = inject(GuardiansService);

  protected readonly stats = [
    { label: 'Active tasks', value: '18', trend: '+4 this week' },
    { label: 'Open requests', value: '7', trend: '2 need review' },
    { label: 'Team notes', value: '24', trend: '6 new today' }
  ];

  protected readonly activity = [
    'Project kickoff checklist updated',
    'Access request routed for approval',
    'Weekly dashboard snapshot prepared'
  ];

  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly childrenLoading = signal(true);
  protected readonly childrenError = signal<string | null>(null);

  protected readonly newChildName = signal('');
  protected readonly addingChild = signal(false);
  protected readonly addChildError = signal<string | null>(null);
  protected readonly lastCreatedChild = signal<CreateChildResult | null>(null);

  ngOnInit(): void {
    void this.loadChildren();
  }

  protected async addChild(): Promise<void> {
    const name = this.newChildName().trim();

    if (!name) {
      return;
    }

    this.addingChild.set(true);
    this.addChildError.set(null);

    try {
      const created = await this.guardians.createChild(name);
      this.lastCreatedChild.set(created);
      this.newChildName.set('');
      await this.loadChildren();
    } catch {
      this.addChildError.set('Unable to create the child account.');
    } finally {
      this.addingChild.set(false);
    }
  }

  protected logout(): void {
    this.auth.logout();
  }

  private async loadChildren(): Promise<void> {
    this.childrenLoading.set(true);
    this.childrenError.set(null);

    try {
      this.children.set(await this.guardians.listMyChildren());
    } catch {
      this.childrenError.set('Unable to load children.');
    } finally {
      this.childrenLoading.set(false);
    }
  }
}

