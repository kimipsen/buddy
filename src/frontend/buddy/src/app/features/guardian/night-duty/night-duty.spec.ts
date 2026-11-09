import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { BabysittersService } from '../../../core/babysitters.service';
import { FeaturesService } from '../../../core/features.service';
import { GuardiansService } from '../../../core/guardians.service';
import { NightDutyService } from '../../../core/night-duty.service';
import { GuardianNightDuty } from './night-duty';

describe('GuardianNightDuty', () => {
  const emil = { id: 'emil', name: { givenName: 'Emil', familyName: 'Holm' } };
  const ida = { id: 'ida', name: { givenName: 'Ida', familyName: 'Holm' } };

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  async function setup(
    options: {
      children?: unknown[];
      enabled?: (name: string) => boolean;
      getSchedule?: () => Promise<unknown>;
    } = {},
  ) {
    const guardians = {
      listMyChildren: vi.fn(async () => options.children ?? [emil, ida]),
      listChildGuardians: vi.fn(async () => []),
    };
    const babysitters = {
      listForChild: vi.fn(async () => {
        throw new Error('500');
      }),
    };
    const nightDuty = {
      getSchedule: vi.fn(
        options.getSchedule ??
          (async () => ({ childId: 'emil', today: '2026-11-09', patterns: [] })),
      ),
      listNights: vi.fn(async () => []),
    };

    await TestBed.configureTestingModule({
      imports: [GuardianNightDuty],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardians },
        { provide: BabysittersService, useValue: babysitters },
        { provide: NightDutyService, useValue: nightDuty },
        { provide: FeaturesService, useValue: { enabled: options.enabled ?? (() => true) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianNightDuty);
    await settle(fixture);
    return {
      fixture,
      compiled: fixture.nativeElement as HTMLElement,
      guardians,
      babysitters,
      nightDuty,
    };
  }

  it("loads the first child's schedule, links to pickups and babysitters, and survives a babysitter failure", async () => {
    const { compiled, nightDuty, guardians } = await setup();

    expect(compiled.textContent).toContain("Who's on duty at night.");
    expect(compiled.textContent).toContain('Who puts Emil to bed');
    expect(guardians.listChildGuardians).toHaveBeenCalledWith('emil');
    expect(nightDuty.getSchedule).toHaveBeenCalledWith('emil');
    expect(compiled.querySelector('app-night-week')).toBeTruthy();
    expect(compiled.querySelector('app-night-pattern-editor')).toBeTruthy();
    expect(compiled.querySelector('a[href="/guardian/pickup"]')?.textContent).toContain(
      'Pickup & drop-off',
    );
    expect(compiled.querySelector('a[href="/guardian/babysitters"]')).toBeTruthy();
  });

  it('switches child', async () => {
    const { fixture, compiled, nightDuty } = await setup();

    const select = compiled.querySelector<HTMLSelectElement>('#night-duty-child')!;
    select.value = select.options[1]!.value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(nightDuty.getSchedule).toHaveBeenLastCalledWith('ida');
    expect(compiled.textContent).toContain('Who puts Ida to bed');
  });

  it('hides the child picker and the links of turned-off features', async () => {
    const { compiled, babysitters } = await setup({
      children: [emil],
      enabled: (name) => name !== 'babysitters' && name !== 'pickups',
    });

    expect(compiled.querySelector('#night-duty-child')).toBeNull();
    expect(compiled.querySelector('a[href="/guardian/pickup"]')).toBeNull();
    expect(compiled.querySelector('a[href="/guardian/babysitters"]')).toBeNull();
    expect(babysitters.listForChild).not.toHaveBeenCalled();
  });

  it('asks for a child first', async () => {
    const { compiled } = await setup({ children: [] });

    expect(compiled.textContent).toContain('Add a child first to plan their nights.');
  });

  it('shows a load error', async () => {
    const { compiled } = await setup({
      getSchedule: async () => {
        throw new Error('500');
      },
    });

    expect(compiled.textContent).toContain('Unable to load night duty.');
  });
});
