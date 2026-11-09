import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildClockService } from '../../../../core/child-clock.service';
import { FeaturesService } from '../../../../core/features.service';
import { GuardiansService } from '../../../../core/guardians.service';
import { Night, NightDutyService } from '../../../../core/night-duty.service';
import { UsersService } from '../../../../core/users.service';
import { TonightCard } from './tonight-card';

describe('TonightCard', () => {
  const dad = {
    id: 'dad',
    name: { givenName: 'Jonas', familyName: 'Holm' },
    callingName: 'Dad',
    kind: 'Parent',
  };

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 4; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function planned(responsible: object, note = ''): Night[] {
    return [
      {
        date: '2026-11-13',
        status: { kind: 2, entry: { responsible, note }, source: 'Pattern', scheduled: null },
      } as Night,
    ];
  }

  async function setup(
    nights: () => Promise<Night[]>,
    options: { enabled?: boolean; guardians?: () => Promise<unknown[]> } = {},
  ) {
    const listNights = vi.fn(nights);
    await TestBed.configureTestingModule({
      imports: [TonightCard],
      providers: [
        { provide: NightDutyService, useValue: { listNights } },
        {
          provide: GuardiansService,
          useValue: { listMyGuardians: vi.fn(options.guardians ?? (async () => [dad])) },
        },
        {
          provide: UsersService,
          useValue: { ensureCurrentUser: vi.fn(async () => ({ id: 'emil' })) },
        },
        { provide: ChildClockService, useValue: { today: () => '2026-11-13' } },
        { provide: FeaturesService, useValue: { enabled: () => options.enabled ?? true } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(TonightCard);
    await settle(fixture);
    return { fixture, compiled: fixture.nativeElement as HTMLElement, listNights };
  }

  it("shows tonight's guardian by the name the child uses, and the note", async () => {
    const { compiled, listNights } = await setup(async () =>
      planned({ kind: 0, guardianId: 'dad' }, 'Screen night 📱'),
    );

    expect(listNights).toHaveBeenCalledWith('emil', '2026-11-13', '2026-11-13');
    const card = compiled.querySelector('[data-testid="tonight-card"]')!;
    expect(card.textContent).toContain('Tonight');
    expect(card.textContent).toContain('Dad');
    expect(card.textContent).toContain('puts you to bed tonight');
    expect(card.textContent).toContain('Screen night 📱');
  });

  it('falls back to a generic word when the guardian is unknown', async () => {
    const { compiled } = await setup(async () => planned({ kind: 0, guardianId: 'gone' }), {
      guardians: async () => {
        throw new Error('500');
      },
    });

    expect(compiled.textContent).toContain('A grown-up');
  });

  it('shows a babysitter and a sleepover', async () => {
    const babysitter = await setup(async () =>
      planned({ kind: 1, guardianId: 'mom', babysitterId: 'b', name: 'Maja' }),
    );
    expect(babysitter.compiled.textContent).toContain('Maja');
    expect(babysitter.compiled.textContent).toContain('is with you tonight');
    TestBed.resetTestingModule();

    const unnamed = await setup(async () =>
      planned({ kind: 1, guardianId: 'mom', babysitterId: 'b', name: '' }),
    );
    expect(unnamed.compiled.textContent).toContain('Your babysitter');
    TestBed.resetTestingModule();

    const sleepover = await setup(async () =>
      planned({ kind: 2, hostName: 'Grandma Lis', location: '', contactInfo: '' }),
    );
    expect(sleepover.compiled.textContent).toContain('You sleep over at Grandma Lis’s');
  });

  it('shows a note on its own without a person', async () => {
    const { compiled } = await setup(async () => planned({ kind: 3 }, 'Movie night'));

    expect(compiled.textContent).toContain('Movie night');
    expect(compiled.textContent).not.toContain('puts you to bed');
  });

  it('renders nothing for an empty night, a failed load or a turned-off feature', async () => {
    const empty = await setup(async () => [{ date: '2026-11-13', status: { kind: 0 } }]);
    expect(empty.compiled.querySelector('[data-testid="tonight-card"]')).toBeNull();
    TestBed.resetTestingModule();

    const failed = await setup(async () => {
      throw new Error('500');
    });
    expect(failed.compiled.querySelector('[data-testid="tonight-card"]')).toBeNull();
    TestBed.resetTestingModule();

    const off = await setup(async () => planned({ kind: 3 }, 'x'), { enabled: false });
    expect(off.listNights).not.toHaveBeenCalled();
    expect(off.compiled.querySelector('[data-testid="tonight-card"]')).toBeNull();
  });
});
