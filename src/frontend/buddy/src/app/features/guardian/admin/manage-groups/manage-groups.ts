import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GroupSummary, GroupsService } from '../../../../core/groups.service';

const ROLE_LABELS: Record<number, string> = {
  0: 'Owner',
  1: 'Admin',
  2: 'Member'
};

@Component({
  selector: 'app-manage-groups',
  imports: [FormsModule],
  templateUrl: './manage-groups.html'
})
export class ManageGroups implements OnInit {
  private readonly groups = inject(GroupsService);

  protected readonly roleLabels = ROLE_LABELS;

  protected readonly items = signal<GroupSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly newGroupName = signal('');
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadGroups();
  }

  protected async createGroup(): Promise<void> {
    const name = this.newGroupName().trim();

    if (!name) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.groups.createGroup({ name });
      this.newGroupName.set('');
      await this.loadGroups();
    } catch {
      this.createError.set('Unable to create the group.');
    } finally {
      this.creating.set(false);
    }
  }

  private async loadGroups(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.items.set(await this.groups.listMyGroups());
    } catch {
      this.error.set('Unable to load groups.');
    } finally {
      this.loading.set(false);
    }
  }
}
