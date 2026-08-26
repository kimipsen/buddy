import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GuardiansService } from '../../../core/guardians.service';
import { PickupsService } from '../../../core/pickups.service';
import { GuardianPickup } from './pickup';

// GuardianPickup is a trivial shell: a back link plus <app-manage-pickups>, no logic of its own.
// This smoke test only confirms it renders that composition -- ManagePickups' own behavior is
// covered by manage-pickups.spec.ts. Its child services still need stubs here, since mounting the
// real ManagePickups otherwise instantiates the real (HttpClient-backed) services via DI.
describe('GuardianPickup', () => {
  async function setup() {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [])
    };
    const pickupsStub: Partial<PickupsService> = {
      listSchedule: vi.fn(async () => [])
    };

    await TestBed.configureTestingModule({
      imports: [GuardianPickup],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: PickupsService, useValue: pickupsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianPickup);

    return { fixture };
  }

  it('renders the manage-pickups panel and a back link to the guardian home', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-manage-pickups')).toBeTruthy();
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();
  });
});
