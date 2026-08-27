import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ManageTasks } from './manage-tasks/manage-tasks';

// Thin page shell mirroring GuardianMedicine exactly: a back link plus <app-manage-tasks>, no
// logic of its own -- ManageTasks does its own child loading/selection (see manage-tasks.ts).
@Component({
  selector: 'app-guardian-task-library',
  imports: [RouterLink, ManageTasks, TranslatePipe],
  templateUrl: './task-library.html'
})
export class GuardianTaskLibrary {}
