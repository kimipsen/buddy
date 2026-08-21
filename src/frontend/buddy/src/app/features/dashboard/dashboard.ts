import { JsonPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';

import { AuthService } from '../../core/auth.service';
import { UserEventItem, UserEventsService } from '../../core/user-events.service';

@Component({
  selector: 'app-dashboard',
  imports: [JsonPipe],
  templateUrl: './dashboard.html'
})
export class Dashboard implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly userEvents = inject(UserEventsService);

  protected readonly stats = [
    { label: 'Active tasks', value: '18', trend: '+4 this week' },
    { label: 'Open requests', value: '7', trend: '2 need review' },
    { label: 'Team notes', value: '24', trend: '6 new today' }
  ];

  protected readonly events = signal<UserEventItem[]>([]);
  protected readonly eventsLoading = signal(true);
  protected readonly eventsError = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadEvents();
  }

  private async loadEvents(): Promise<void> {
    this.eventsLoading.set(true);
    this.eventsError.set(null);

    try {
      this.events.set(await this.userEvents.listCurrentUserEvents());
    } catch {
      this.eventsError.set('Unable to load recent events.');
    } finally {
      this.eventsLoading.set(false);
    }
  }

  protected readonly activity = [
    'Project kickoff checklist updated',
    'Access request routed for approval',
    'Weekly dashboard snapshot prepared'
  ];

  protected logout(): void {
    this.auth.logout();
  }
}
