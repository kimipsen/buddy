import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ManageProgressGoals } from './manage-progress-goals/manage-progress-goals';

@Component({
  selector: 'app-guardian-progress',
  imports: [RouterLink, ManageProgressGoals, TranslatePipe],
  templateUrl: './progress.html'
})
export class GuardianProgress {}
