import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ManageMedicines } from './manage-medicines/manage-medicines';
import { Page } from '../../../shared/page/page';

@Component({
  selector: 'app-guardian-medicine',
  imports: [ManageMedicines, RouterLink, TranslatePipe, Page],
  templateUrl: './medicine.html',
})
export class GuardianMedicine {}
