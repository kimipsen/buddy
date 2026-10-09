import { Component } from '@angular/core';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ManageTasks } from './manage-tasks/manage-tasks';
import { Page } from '../../../shared/page/page';

// Thin page shell mirroring GuardianMedicine exactly: a back link plus <app-manage-tasks>, no
// logic of its own -- ManageTasks does its own child loading/selection (see manage-tasks.ts).
@Component({
  selector: 'app-guardian-task-library',
  imports: [ManageTasks, TranslatePipe, Page],
  templateUrl: './task-library.html',
})
export class GuardianTaskLibrary {}
