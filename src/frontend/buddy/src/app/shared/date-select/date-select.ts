import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

// `value`/`valueChange` carry a plain "YYYY-MM-DD" string, the same shape the calendar item APIs
// already use and the native <input type="date"> value format.
@Component({
  selector: 'app-date-select',
  imports: [FormsModule],
  templateUrl: './date-select.html'
})
export class DateSelect {
  readonly value = input<string>('');
  readonly valueChange = output<string>();
}
