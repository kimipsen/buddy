import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ProfileMenu } from './profile-menu/profile-menu';

@Component({
  selector: 'app-guardian-shell',
  imports: [RouterOutlet, RouterLink, ProfileMenu, TranslatePipe],
  templateUrl: './guardian-shell.html'
})
export class GuardianShell {}
