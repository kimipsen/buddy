import { Component, input, output } from '@angular/core';

import { TimeSelect } from '../time-select/time-select';

// A start -> end window of wall-clock times (e.g. the Sleep Diary's bedtime ritual), the composite
// control docs/frontend/analysis/visual-specification.md calls for instead of two unlinked
// app-time-selects. Both ends are plain "HH:mm" strings ('' for blank) and either may be left
// blank; nothing enforces start < end, since a window can cross midnight.
@Component({
  selector: 'app-time-range',
  imports: [TimeSelect],
  templateUrl: './time-range.html',
})
export class TimeRange {
  readonly start = input<string>('');
  readonly end = input<string>('');
  readonly startLabel = input.required<string>();
  readonly endLabel = input.required<string>();

  readonly startChange = output<string>();
  readonly endChange = output<string>();
}
