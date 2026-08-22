import { Component, inject } from '@angular/core';

import { AuthService } from '../../core/auth.service';
import { EventsList } from './events-list/events-list';

@Component({
  selector: 'app-dashboard',
  imports: [EventsList],
  templateUrl: './dashboard.html'
})
export class Dashboard {
  private readonly auth = inject(AuthService);

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

  protected logout(): void {
    this.auth.logout();
  }
}
