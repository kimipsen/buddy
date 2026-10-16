import { Pipe, PipeTransform } from '@angular/core';

import { iconText } from '../../core/pictograms';

// For an icon where only text fits, such as a <select> option: see iconText.
@Pipe({ name: 'iconText' })
export class IconTextPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return iconText(value);
  }
}
