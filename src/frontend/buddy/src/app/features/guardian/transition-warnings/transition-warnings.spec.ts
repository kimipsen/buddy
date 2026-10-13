import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { disableFeatures } from '../../../../testing/features-fixture';
import { CalendarsService } from '../../../core/calendars.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import {
  ChildTransitionWarnings,
  TransitionWarningsService,
} from '../../../core/transition-warnings.service';
import { GuardianTransitionWarnings } from './transition-warnings';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function child(id: string, givenName: string): ChildSummary {
  return { id, name: { givenName, familyName: 'Hansen' } } as ChildSummary;
}

function settings(overrides: Partial<ChildTransitionWarnings> = {}): ChildTransitionWarnings {
  return {
    childId: 'c1',
    enabled: false,
    leadMinutes: [15, 5, 1],
    kinds: ['Event', 'Task', 'DropOff', 'PickUp'],
    quietHours: null,
    overrides: [],
    ...overrides,
  };
}

describe('GuardianTransitionWarnings', () => {
  async function setup(
    options: {
      stored?: ChildTransitionWarnings;
      childParam?: string;
      children?: ChildSummary[];
    } = {},
  ) {
    const api = {
      getForChild: vi.fn(async (childId: string) => ({
        ...(options.stored ?? settings()),
        childId,
      })),
      configure: vi.fn(async (childId: string, body: Partial<ChildTransitionWarnings>) =>
        settings({
          ...body,
          childId,
          quietHours: body.quietHours ?? null,
          overrides: options.stored?.overrides ?? [],
        }),
      ),
      setOverride: vi.fn(),
      clearOverride: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [GuardianTransitionWarnings],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(
                options.childParam ? { child: options.childParam } : {},
              ),
            },
          },
        },
        {
          provide: GuardiansService,
          useValue: {
            listMyChildren: vi.fn(
              async () => options.children ?? [child('c1', 'Emil'), child('c2', 'Ida')],
            ),
          },
        },
        { provide: TransitionWarningsService, useValue: api },
        { provide: CalendarsService, useValue: { listOccurrencesInRange: vi.fn(async () => []) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianTransitionWarnings);
    await settle(fixture);
    return { fixture, element: fixture.nativeElement as HTMLElement, api };
  }

  function mainSwitch(element: HTMLElement): HTMLButtonElement {
    return element.querySelector('button[role="switch"]')!;
  }

  function button(element: HTMLElement, text: string): HTMLButtonElement | undefined {
    return [...element.querySelectorAll('button')].find((b) => b.textContent?.trim() === text) as
      HTMLButtonElement | undefined;
  }

  it('shows only the switch, an explanation and an example while warnings are off', async () => {
    const { element, api } = await setup();

    expect(api.getForChild).toHaveBeenCalledWith('c1');
    expect(element.textContent).toContain('Warn Emil before changes');
    expect(mainSwitch(element).getAttribute('aria-checked')).toBe('false');
    expect(element.textContent).toContain('Heads-ups are off for Emil.');
    expect(element.textContent).toContain('15 min, 5 min and 1 min before football');
    expect(element.textContent).not.toContain('Quiet hours');
    expect(button(element, 'Save')).toBeUndefined();
    expect(element.querySelector('app-warning-exceptions')).toBeNull();
  });

  it('reveals the form pre-filled with 15/5/1 and every kind, sending nothing until Save', async () => {
    const { fixture, element, api } = await setup();

    mainSwitch(element).click();
    await settle(fixture);

    const pressed = [...element.querySelectorAll('app-lead-chips button[aria-pressed="true"]')];
    expect(pressed.map((b) => b.textContent?.trim())).toEqual(['15 min', '5 min', '1 min']);
    const kinds = [...element.querySelectorAll('ul button[role="switch"]')];
    expect(kinds.map((k) => k.getAttribute('aria-checked'))).toEqual([
      'true',
      'true',
      'true',
      'true',
    ]);
    expect(element.querySelector('[data-testid="warning-preview"]')?.textContent?.trim()).toBe(
      'Emil sees a heads-up 15 min, 5 min, and 1 min before each change.',
    );
    expect(api.configure).not.toHaveBeenCalled();

    button(element, 'Save')?.click();
    await settle(fixture);

    expect(api.configure).toHaveBeenCalledWith('c1', {
      enabled: true,
      leadMinutes: [15, 5, 1],
      kinds: ['Event', 'Task', 'DropOff', 'PickUp'],
      quietHours: null,
    });
    expect(element.textContent).toContain('Saved.');
    expect(element.querySelector('app-warning-exceptions')).not.toBeNull();
  });

  it('saves changed lead times, kinds and quiet hours', async () => {
    const { fixture, element, api } = await setup({ stored: settings({ enabled: true }) });

    const chip = (text: string) =>
      [...element.querySelectorAll('app-lead-chips button')].find(
        (b) => b.textContent?.trim() === text,
      ) as HTMLButtonElement;
    chip('1 min').click();
    await settle(fixture);
    chip('10 min').click();
    await settle(fixture);
    // Three chosen: the others can't be pressed.
    expect(chip('30 min').disabled).toBe(true);

    const kindSwitches = [...element.querySelectorAll('ul button[role="switch"]')];
    (kindSwitches[1] as HTMLButtonElement).click();
    const quietSwitch = [...element.querySelectorAll('button[role="switch"]')].find(
      (b) => b.getAttribute('aria-label') === 'Quiet hours',
    ) as HTMLButtonElement;
    quietSwitch.click();
    await settle(fixture);

    button(element, 'Save')?.click();
    await settle(fixture);

    expect(api.configure).toHaveBeenCalledWith('c1', {
      enabled: true,
      leadMinutes: [15, 10, 5],
      kinds: ['Event', 'DropOff', 'PickUp'],
      quietHours: { from: '21:00', to: '07:00' },
    });
  });

  it('switching off and saving keeps the other values', async () => {
    const { fixture, element, api } = await setup({
      stored: settings({ enabled: true, leadMinutes: [20], kinds: ['Event'] }),
    });

    mainSwitch(element).click();
    await settle(fixture);
    expect(element.textContent).toContain('Heads-ups are off for Emil.');
    button(element, 'Save')?.click();
    await settle(fixture);

    expect(api.configure).toHaveBeenCalledWith('c1', {
      enabled: false,
      leadMinutes: [20],
      kinds: ['Event'],
      quietHours: null,
    });
  });

  it('shows an error when saving fails', async () => {
    const { fixture, element, api } = await setup({ stored: settings({ enabled: true }) });
    api.configure.mockRejectedValueOnce(new Error('offline'));

    const chip = [...element.querySelectorAll('app-lead-chips button')].find(
      (b) => b.textContent?.trim() === '1 min',
    ) as HTMLButtonElement;
    chip.click();
    await settle(fixture);
    button(element, 'Save')?.click();
    await settle(fixture);

    expect(element.textContent).toContain('Couldn’t save. Try again.');
  });

  it('opens on the child named in ?child=', async () => {
    const { element, api } = await setup({ childParam: 'c2' });

    expect(api.getForChild).toHaveBeenCalledWith('c2');
    expect(element.textContent).toContain('Warn Ida before changes');
  });

  it('switches child from the picker', async () => {
    const { fixture, element, api } = await setup();

    const select = element.querySelector('select') as HTMLSelectElement;
    select.value = 'c2';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(api.getForChild).toHaveBeenLastCalledWith('c2');
    expect(element.textContent).toContain('Warn Ida before changes');
  });

  it('hides the drop-off and pickup kinds while pickups are off', async () => {
    disableFeatures('pickups');
    const { element } = await setup({ stored: settings({ enabled: true }) });

    const labels = [...element.querySelectorAll('ul li > span')].map((s) => s.textContent?.trim());
    expect(labels).toEqual(['Events', 'Tasks and routines']);
  });

  it("won't save warnings on with only hidden kinds left", async () => {
    disableFeatures('pickups');
    const { fixture, element } = await setup({ stored: settings({ enabled: true }) });

    const kindSwitches = [...element.querySelectorAll('ul button[role="switch"]')];
    (kindSwitches[0] as HTMLButtonElement).click();
    await settle(fixture);
    (kindSwitches[1] as HTMLButtonElement).click();
    await settle(fixture);

    expect(button(element, 'Save')?.disabled).toBe(true);
  });

  it('asks for a child first when there is none', async () => {
    const { element, api } = await setup({ children: [] });

    expect(element.textContent).toContain('Add a child first');
    expect(api.getForChild).not.toHaveBeenCalled();
  });
});
