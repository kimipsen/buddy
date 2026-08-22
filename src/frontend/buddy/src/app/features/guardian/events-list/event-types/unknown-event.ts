import { JsonPipe } from '@angular/common';
import { Component, input } from '@angular/core';

@Component({
  selector: 'app-unknown-event',
  imports: [JsonPipe],
  templateUrl: './unknown-event.html'
})
export class UnknownEvent {
  readonly type = input.required<string>();
  readonly data = input.required<Record<string, unknown>>();
}
