import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { CalendarAgenda } from './agenda/agenda';

@Component({
  selector: 'app-guardian-calendar',
  imports: [RouterLink, CalendarAgenda, TranslatePipe],
  templateUrl: './calendar.html'
})
export class GuardianCalendar {}
