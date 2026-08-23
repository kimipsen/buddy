import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CurrentUser, UsersService } from '../../../../core/users.service';

@Component({
  selector: 'app-my-profile',
  imports: [FormsModule],
  templateUrl: './my-profile.html'
})
export class MyProfile implements OnInit {
  private readonly users = inject(UsersService);

  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly currentEmail = signal<string | null>(null);

  protected readonly givenName = signal('');
  protected readonly familyName = signal('');
  protected readonly savingName = signal(false);
  protected readonly nameError = signal<string | null>(null);
  protected readonly nameSaved = signal(false);

  protected readonly email = signal('');
  protected readonly savingEmail = signal(false);
  protected readonly emailError = signal<string | null>(null);
  protected readonly emailSaved = signal(false);

  ngOnInit(): void {
    void this.loadProfile();
  }

  protected async saveName(): Promise<void> {
    const givenName = this.givenName().trim();
    const familyName = this.familyName().trim();

    if (!givenName || !familyName) {
      return;
    }

    this.savingName.set(true);
    this.nameError.set(null);
    this.nameSaved.set(false);

    try {
      await this.users.updateName(givenName, familyName);
      this.nameSaved.set(true);
    } catch {
      this.nameError.set('Unable to update your name.');
    } finally {
      this.savingName.set(false);
    }
  }

  protected async saveEmail(): Promise<void> {
    const email = this.email().trim();

    if (!email) {
      return;
    }

    this.savingEmail.set(true);
    this.emailError.set(null);
    this.emailSaved.set(false);

    try {
      const updated = await this.users.updateEmail(email);
      this.currentEmail.set(updated.email.value);
      this.emailSaved.set(true);
    } catch (error) {
      this.emailError.set(
        error instanceof HttpErrorResponse && typeof error.error === 'string'
          ? error.error
          : 'Unable to update your email.'
      );
    } finally {
      this.savingEmail.set(false);
    }
  }

  private async loadProfile(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      this.applyCurrentUser(await this.users.ensureCurrentUser());
    } catch {
      this.loadError.set('Unable to load your profile.');
    } finally {
      this.loading.set(false);
    }
  }

  private applyCurrentUser(user: CurrentUser): void {
    this.givenName.set(user.name.givenName);
    this.familyName.set(user.name.familyName);
    this.email.set(user.email.value);
    this.currentEmail.set(user.email.value);
  }
}
