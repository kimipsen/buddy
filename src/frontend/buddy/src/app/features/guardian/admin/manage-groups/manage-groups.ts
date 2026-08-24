import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';

const ROLE_LABELS: Record<number, string> = {
  0: 'admin.manageGroups.roles.owner',
  1: 'admin.manageGroups.roles.admin',
  2: 'admin.manageGroups.roles.member'
};

@Component({
  selector: 'app-manage-groups',
  imports: [FormsModule, TranslatePipe],
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
      this.createError.set('admin.manageGroups.createError');
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
      this.error.set('admin.manageGroups.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
