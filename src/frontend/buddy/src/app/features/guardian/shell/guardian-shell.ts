import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { ProfileMenu } from './profile-menu/profile-menu';

@Component({
  selector: 'app-guardian-shell',
  imports: [RouterOutlet, RouterLink, ProfileMenu],
  templateUrl: './guardian-shell.html'
})
export class GuardianShell {}
