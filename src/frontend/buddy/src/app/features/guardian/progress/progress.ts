import { Component } from '@angular/core';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ManageProgressGoals } from './manage-progress-goals/manage-progress-goals';
import { Page } from '../../../shared/page/page';

@Component({
  selector: 'app-guardian-progress',
  imports: [ManageProgressGoals, TranslatePipe, Page],
  templateUrl: './progress.html',
})
export class GuardianProgress {}
