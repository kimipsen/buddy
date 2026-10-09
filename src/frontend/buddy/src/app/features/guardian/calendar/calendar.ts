import { Component } from '@angular/core';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { CalendarAgenda } from './agenda/agenda';
import { Page } from '../../../shared/page/page';

@Component({
  selector: 'app-guardian-calendar',
  imports: [CalendarAgenda, TranslatePipe, Page],
  templateUrl: './calendar.html',
})
export class GuardianCalendar {}
