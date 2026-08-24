import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ManageMedicines } from './manage-medicines/manage-medicines';

@Component({
  selector: 'app-guardian-medicine',
  imports: [RouterLink, ManageMedicines],
  templateUrl: './medicine.html'
})
export class GuardianMedicine {}
