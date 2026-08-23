import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ChildSummary, CreateChildResult, GuardiansService } from '../../../../core/guardians.service';

@Component({
  selector: 'app-manage-children',
  imports: [FormsModule],
  templateUrl: './manage-children.html'
})
export class ManageChildren implements OnInit {
  private readonly guardians = inject(GuardiansService);

  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly childrenLoading = signal(true);
  protected readonly childrenError = signal<string | null>(null);

  protected readonly newChildGivenName = signal('');
  protected readonly newChildFamilyName = signal('');
  protected readonly newChildUsername = signal('');
  protected readonly addingChild = signal(false);
  protected readonly addChildError = signal<string | null>(null);
  protected readonly lastCreatedChild = signal<CreateChildResult | null>(null);

  protected readonly revokingChildId = signal<string | null>(null);
  protected readonly confirmingRevokeChildId = signal<string | null>(null);
  protected readonly revokeError = signal<string | null>(null);

  protected readonly passwordCopied = signal(false);

  ngOnInit(): void {
    void this.loadChildren();
  }

  protected async addChild(): Promise<void> {
    const givenName = this.newChildGivenName().trim();
    const familyName = this.newChildFamilyName().trim();
    const username = this.newChildUsername().trim();

    if (!givenName || !familyName || !username) {
      return;
    }

    this.addingChild.set(true);
    this.addChildError.set(null);

    try {
      const created = await this.guardians.createChild({ givenName, familyName, username });
      this.lastCreatedChild.set(created);
      this.passwordCopied.set(false);
      this.newChildGivenName.set('');
      this.newChildFamilyName.set('');
      this.newChildUsername.set('');
      await this.loadChildren();
    } catch (error) {
      this.addChildError.set(error instanceof HttpErrorResponse && error.status === 409
        ? 'That username is already in use. Choose another one.'
        : 'Unable to create the child account.');
    } finally {
      this.addingChild.set(false);
    }
  }

  protected requestRevoke(childId: string): void {
    this.revokeError.set(null);
    this.confirmingRevokeChildId.set(childId);
  }

  protected cancelRevoke(): void {
    this.confirmingRevokeChildId.set(null);
  }

  protected async confirmRevoke(childId: string): Promise<void> {
    this.revokingChildId.set(childId);
    this.revokeError.set(null);

    try {
      await this.guardians.revokeChild(childId);
      this.confirmingRevokeChildId.set(null);
      await this.loadChildren();
    } catch {
      this.revokeError.set('Unable to remove this child.');
    } finally {
      this.revokingChildId.set(null);
    }
  }

  protected async copyPassword(password: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(password);
      this.passwordCopied.set(true);
    } catch {
      this.passwordCopied.set(false);
    }
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
