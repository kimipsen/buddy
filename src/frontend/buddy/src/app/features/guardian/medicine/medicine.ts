import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ManageMedicines } from './manage-medicines/manage-medicines';

@Component({
  selector: 'app-guardian-medicine',
  imports: [RouterLink, ManageMedicines, TranslatePipe],
  templateUrl: './medicine.html'
})
export class GuardianMedicine {}
