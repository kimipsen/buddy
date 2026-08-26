import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GroupsService } from '../../../core/groups.service';
import { GuardiansService } from '../../../core/guardians.service';
import { MedicinesService } from '../../../core/medicines.service';
import { GuardianMedicine } from './medicine';

// GuardianMedicine is a trivial shell: a back link plus <app-manage-medicines>, no logic of its
// own. This smoke test only confirms it renders that composition -- ManageMedicines' own behavior
// is covered by manage-medicines.spec.ts. Its child services still need stubs here, since mounting
// the real ManageMedicines otherwise instantiates the real (HttpClient-backed) services via DI.
describe('GuardianMedicine', () => {
  async function setup() {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [])
    };
    const medicinesStub: Partial<MedicinesService> = {
      listSchedules: vi.fn(async () => [])
    };
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => [])
    };

    await TestBed.configureTestingModule({
      imports: [GuardianMedicine],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: MedicinesService, useValue: medicinesStub },
        { provide: GroupsService, useValue: groupsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianMedicine);

    return { fixture };
  }

  it('renders the manage-medicines panel and a back link to the guardian home', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-manage-medicines')).toBeTruthy();
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();
  });
});
