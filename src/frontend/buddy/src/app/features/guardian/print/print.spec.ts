import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { GroupsService } from '../../../core/groups.service';
import { PrintTemplate, PrintTemplatesService } from '../../../core/print-templates.service';
import { GuardianPrint } from './print';

describe('GuardianPrint', () => {
  const template = (
    id: string,
    defaultStartWeekday: PrintTemplate['defaultStartWeekday'],
  ): PrintTemplate => ({
    id,
    ownerUserId: 'me',
    ownerGroupId: null,
    name: id,
    paperSize: 0,
    defaultStartWeekday,
    showWeekNumber: true,
    rows: [],
    guardianColors: [],
  });

  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    for (let i = 0; i < 4; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function button(root: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === text)!;
  }

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 1, 12)); // Thursday
    localStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
    localStorage.clear();
  });

  async function setup(
    list = [
      { id: 'school', ownerUserId: 'me', ownerGroupId: null, name: 'School week' },
      { id: 'family', ownerUserId: null, ownerGroupId: 'g-1', name: 'Family week' },
    ],
  ) {
    const service = {
      list: vi.fn(async () => list),
      get: vi.fn(async (id: string) => template(id, id === 'family' ? 0 : 1)),
      create: vi.fn(async () => template('new', 1)),
    };

    await TestBed.configureTestingModule({
      imports: [GuardianPrint],
      providers: [
        provideRouter([]),
        { provide: PrintTemplatesService, useValue: service },
        {
          provide: GroupsService,
          useValue: { listMyGroups: vi.fn(async () => [{ id: 'g-1', name: 'Familien', role: 0 }]) },
        },
      ],
    }).compileComponents();

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(GuardianPrint);
    await settle(fixture);

    return { fixture, service, navigate, root: fixture.nativeElement as HTMLElement };
  }

  it('preselects the first template and its next default start weekday, then previews it', async () => {
    const { fixture, root, navigate } = await setup();

    expect(root.querySelector<HTMLInputElement>('app-date-select input')!.value).toBe('2026-10-05');
    expect(root.textContent).toContain('Family week');
    expect(root.textContent).toContain('(Familien)');

    button(root, 'Preview').click();
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith(['/guardian/print/sheet', 'school'], {
      queryParams: { start: '2026-10-05' },
    });
  });

  it('remembers the last template on this device', async () => {
    localStorage.setItem('buddy_print_last_template', 'family');

    const { root } = await setup();

    expect(root.querySelector<HTMLSelectElement>('#print-template')!.value).toBe('family');
    // The family template starts on Sunday.
    expect(root.querySelector<HTMLInputElement>('app-date-select input')!.value).toBe('2026-10-04');
  });

  it('switching template updates the suggested start date and remembers the choice', async () => {
    const { fixture, root } = await setup();
    const select = root.querySelector<HTMLSelectElement>('#print-template')!;

    select.value = 'family';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(root.querySelector<HTMLInputElement>('app-date-select input')!.value).toBe('2026-10-04');
    expect(localStorage.getItem('buddy_print_last_template')).toBe('family');
  });

  it('disables Preview while a newly chosen template fails to load, keeping the choice visible', async () => {
    const { fixture, root, service } = await setup();
    service.get.mockRejectedValueOnce(new Error('404'));
    const select = root.querySelector<HTMLSelectElement>('#print-template')!;

    select.value = 'family';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(select.value).toBe('family');
    expect(button(root, 'Preview').disabled).toBe(true);
    expect(root.textContent).toContain('Unable to load your print templates.');
  });

  it('shows the empty hint without templates', async () => {
    const { root } = await setup([]);

    expect(root.textContent).toContain('No templates yet.');
    expect(root.querySelector('#print-template')).toBeNull();
  });

  it('creates a group template and opens it in the editor', async () => {
    const { fixture, root, service, navigate } = await setup();

    const name = root.querySelector<HTMLInputElement>('#new-template-name')!;
    name.value = '  Ferieuge ';
    name.dispatchEvent(new Event('input'));
    const owner = root.querySelector<HTMLSelectElement>('#new-template-owner')!;
    owner.value = 'g-1';
    owner.dispatchEvent(new Event('change'));
    await settle(fixture);
    button(root, 'Create template').click();
    await settle(fixture);

    expect(service.create).toHaveBeenCalledWith('Ferieuge', 'g-1');
    expect(navigate).toHaveBeenCalledWith(['/guardian/print/templates', 'new']);
  });

  it('creates a personal template when "Just me" is chosen', async () => {
    const { fixture, root, service } = await setup();

    const name = root.querySelector<HTMLInputElement>('#new-template-name')!;
    name.value = 'Min uge';
    name.dispatchEvent(new Event('input'));
    await settle(fixture);
    button(root, 'Create template').click();
    await settle(fixture);

    expect(service.create).toHaveBeenCalledWith('Min uge', null);
  });

  it('shows the load error', async () => {
    const service = {
      list: vi.fn(async () => Promise.reject(new Error('500'))),
      get: vi.fn(),
      create: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [GuardianPrint],
      providers: [
        provideRouter([]),
        { provide: PrintTemplatesService, useValue: service },
        { provide: GroupsService, useValue: { listMyGroups: vi.fn(async () => []) } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(GuardianPrint);
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Unable to load your print templates.',
    );
  });
});
