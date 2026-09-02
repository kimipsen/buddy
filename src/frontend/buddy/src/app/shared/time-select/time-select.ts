import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

// `value`/`valueChange` carry a plain "HH:mm" string (24-hour, no seconds), the same shape the
// pickups and medicine-schedule APIs already use and the native <input type="time"> value format.
@Component({
  selector: 'app-time-select',
  imports: [FormsModule],
  templateUrl: './time-select.html'
})
export class TimeSelect {
  readonly value = input<string>('');
  readonly valueChange = output<string>();
  readonly ariaLabel = input('');
}
