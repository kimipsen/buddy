import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UsersService } from '../../../../core/users.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';

// "Your data": downloads GET /users/me/export, everything Buddy holds about the guardian and the
// children they guard, as a JSON file. The request needs the bearer token, so it can't be a plain
// link: the file is fetched as a blob and saved through a temporary object URL.
@Component({
  selector: 'app-download-my-data',
  imports: [TranslatePipe, Card],
  templateUrl: './download-my-data.html',
})
export class DownloadMyData {
  private readonly users = inject(UsersService);
  private readonly document = inject(DOCUMENT);

  protected readonly downloading = createAction();

  protected async download(): Promise<void> {
    await this.downloading.run(
      true,
      async () => this.save(await this.users.downloadPersonalData()),
      (error) =>
        error instanceof HttpErrorResponse && error.status === 429
          ? 'admin.downloadData.rateLimited'
          : 'admin.downloadData.error',
    );
  }

  private save(file: Blob): void {
    const url = URL.createObjectURL(file);
    const link = this.document.createElement('a');
    link.href = url;
    link.download = `buddy-export-${new Date().toISOString().slice(0, 10)}.json`;
    link.click();
    URL.revokeObjectURL(url);
  }
}
