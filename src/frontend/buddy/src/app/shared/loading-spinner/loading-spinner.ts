import { Component, input } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  templateUrl: './loading-spinner.html'
})
export class LoadingSpinner {
  readonly label = input<string | null>(null);
}
