import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { CalendarsService } from '../../../../core/calendars.service';
import { OnboardingSetup } from '../../../../core/onboarding.service';
import { TaskLibraryService } from '../../../../core/task-library.service';
import { addDaysIso, todayIsoDate } from '../../../../core/date-utils';
import {
  buttonByText,
  calendarDetail,
  child,
  settle,
  setupWith,
  typeInto,
  taskTemplate,
} from '../../../../../testing/onboarding-fixture';
import { TaskStep } from './task-step';

const twoSteps = [
  { id: 's1', title: 'Brush teeth', icon: null, durationMinutes: 5 },
  { id: 's2', title: 'Get dressed', icon: null, durationMinutes: 10 },
];

describe('TaskStep', () => {
  async function setup(
    setupState: OnboardingSetup,
    stubs: {
      taskLibrary?: Partial<TaskLibraryService>;
      calendars?: Partial<CalendarsService>;
    } = {},
  ) {
    const taskLibrary: Partial<TaskLibraryService> = {
      createTaskTemplate: vi.fn(async () => taskTemplate({ id: 'template-new' })),
      addSubtask: vi.fn(async () => taskTemplate()),
      ...stubs.taskLibrary,
    };
    const calendars: Partial<CalendarsService> = {
      scheduleTaskFromTemplate: vi.fn(async () => ({}) as never),
      ...stubs.calendars,
    };

    await TestBed.configureTestingModule({
      imports: [TaskStep],
      providers: [
        { provide: TaskLibraryService, useValue: taskLibrary },
        { provide: CalendarsService, useValue: calendars },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(TaskStep);
    fixture.componentRef.setInput('setup', setupState);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);

    return {
      fixture,
      compiled: fixture.nativeElement as HTMLElement,
      taskLibrary,
      calendars,
      changed,
    };
  }

  const base = setupWith({ children: [child()], calendars: [calendarDetail()] });

  async function fillNewRoutine(fixture: Awaited<ReturnType<typeof setup>>['fixture']) {
    const compiled = fixture.nativeElement as HTMLElement;
    typeInto(compiled.querySelector<HTMLInputElement>('#onboardingTaskName')!, 'Morning');
    const [first, second] = Array.from(
      compiled.querySelectorAll<HTMLInputElement>('input[aria-label^="Step "]'),
    );
    typeInto(first!, 'Brush teeth');
    typeInto(second!, 'Get dressed');
    await settle(fixture);
  }

  function saveForm(compiled: HTMLElement) {
    buttonByText(compiled, 'Save routine')!.closest('form')!.dispatchEvent(new Event('submit'));
  }

  it('starts a child with no routines on a new one with two empty steps', async () => {
    const { compiled } = await setup(base);

    expect(compiled.querySelector<HTMLSelectElement>('#onboardingTaskTemplate')!.value).toBe('new');
    expect(compiled.querySelectorAll('input[aria-label^="Step "]').length).toBe(2);
    expect(buttonByText(compiled, 'Save routine')!.disabled).toBe(true);
  });

  it('saves the template and each step in order', async () => {
    const { fixture, compiled, taskLibrary, changed } = await setup(base);

    await fillNewRoutine(fixture);
    saveForm(compiled);
    await settle(fixture);

    expect(taskLibrary.createTaskTemplate).toHaveBeenCalledWith('child-1', {
      name: 'Morning',
      icon: '📋',
      color: '#10b981',
    });
    expect(taskLibrary.addSubtask).toHaveBeenNthCalledWith(
      1,
      'template-new',
      'Brush teeth',
      null,
      5,
    );
    expect(taskLibrary.addSubtask).toHaveBeenNthCalledWith(
      2,
      'template-new',
      'Get dressed',
      null,
      5,
    );
    expect(changed).toHaveBeenCalled();
  });

  it('resumes after a failed step without creating another template', async () => {
    const addSubtask = vi
      .fn()
      .mockResolvedValueOnce(taskTemplate())
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }))
      .mockResolvedValue(taskTemplate());
    const { fixture, compiled, taskLibrary } = await setup(base, { taskLibrary: { addSubtask } });

    await fillNewRoutine(fixture);
    saveForm(compiled);
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to save the routine.');

    saveForm(compiled);
    await settle(fixture);

    expect(taskLibrary.createTaskTemplate).toHaveBeenCalledTimes(1);
    expect(addSubtask).toHaveBeenCalledTimes(3);
    expect(addSubtask).toHaveBeenLastCalledWith('template-new', 'Get dressed', null, 5);
  });

  it('completes an existing template that has too few steps', async () => {
    const { fixture, compiled, taskLibrary } = await setup({
      ...base,
      templates: [{ childId: 'child-1', template: taskTemplate({ subtasks: [twoSteps[0]!] }) }],
    });

    const rows = compiled.querySelectorAll<HTMLInputElement>('input[aria-label^="Step "]');
    expect(rows.length).toBe(1);
    typeInto(rows[0]!, 'Get dressed');
    await settle(fixture);
    saveForm(compiled);
    await settle(fixture);

    expect(taskLibrary.createTaskTemplate).not.toHaveBeenCalled();
    expect(taskLibrary.addSubtask).toHaveBeenCalledWith('template-1', 'Get dressed', null, 5);
  });

  it('schedules a ready routine once on the setup calendar for the child', async () => {
    const { fixture, compiled, calendars, changed } = await setup({
      ...base,
      templates: [{ childId: 'child-1', template: taskTemplate({ subtasks: twoSteps }) }],
    });

    buttonByText(compiled, 'Add to calendar')!.closest('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.scheduleTaskFromTemplate).toHaveBeenCalledTimes(1);
    expect(calendars.scheduleTaskFromTemplate).toHaveBeenCalledWith('cal-1', {
      taskTemplateId: 'template-1',
      startDate: todayIsoDate(),
      startTime: '07:30:00',
      recurrence: null,
      assignedTo: 'child-1',
      title: 'Morning routine',
      icon: '🌅',
      color: '#10b981',
    });
    expect(changed).toHaveBeenCalled();
  });

  it('asks which calendar when the group has several', async () => {
    const { compiled } = await setup({
      ...base,
      calendars: [calendarDetail(), calendarDetail({ id: 'cal-2', name: 'School' })],
      templates: [{ childId: 'child-1', template: taskTemplate({ subtasks: twoSteps }) }],
    });

    expect(compiled.querySelector<HTMLSelectElement>('#onboardingTaskCalendar')!.value).toBe('');
    expect(buttonByText(compiled, 'Add to calendar')!.disabled).toBe(true);
  });

  it('asks which routine when the child has several, never guessing', async () => {
    const { compiled } = await setup({
      ...base,
      templates: [
        { childId: 'child-1', template: taskTemplate({ subtasks: twoSteps }) },
        { childId: 'child-1', template: taskTemplate({ id: 'template-2', subtasks: twoSteps }) },
      ],
    });

    expect(compiled.querySelector<HTMLSelectElement>('#onboardingTaskTemplate')!.value).toBe('');
    expect(buttonByText(compiled, 'Add to calendar')).toBeUndefined();
  });

  it('keeps the template when scheduling fails, so the retry only schedules', async () => {
    const scheduleTaskFromTemplate = vi
      .fn()
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }))
      .mockResolvedValue({});
    const { fixture, compiled, taskLibrary } = await setup(
      {
        ...base,
        templates: [{ childId: 'child-1', template: taskTemplate({ subtasks: twoSteps }) }],
      },
      { calendars: { scheduleTaskFromTemplate } },
    );
    const scheduleForm = () =>
      buttonByText(compiled, 'Add to calendar')!
        .closest('form')!
        .dispatchEvent(new Event('submit'));

    scheduleForm();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to add the routine to the calendar.');

    scheduleForm();
    await settle(fixture);

    expect(scheduleTaskFromTemplate).toHaveBeenCalledTimes(2);
    expect(taskLibrary.createTaskTemplate).not.toHaveBeenCalled();
  });

  it('keeps the routine it just saved selected when the setup reloads with others', async () => {
    const existing = taskTemplate({ subtasks: twoSteps });
    const { fixture, compiled } = await setup({
      ...base,
      templates: [{ childId: 'child-1', template: existing }],
    });
    const choice = compiled.querySelector<HTMLSelectElement>('#onboardingTaskTemplate')!;

    typeInto(choice, 'new');
    await settle(fixture);
    await fillNewRoutine(fixture);
    saveForm(compiled);
    await settle(fixture);
    fixture.componentRef.setInput('setup', {
      ...base,
      templates: [
        { childId: 'child-1', template: existing },
        {
          childId: 'child-1',
          template: taskTemplate({ id: 'template-new', name: 'Morning', subtasks: twoSteps }),
        },
      ],
    });
    await settle(fixture);

    expect(choice.value).toBe('template-new');
    expect(buttonByText(compiled, 'Add to calendar')).toBeDefined();
  });

  it('keeps a chosen calendar when the setup reloads', async () => {
    const state = {
      ...base,
      calendars: [calendarDetail(), calendarDetail({ id: 'cal-2', name: 'School' })],
      templates: [{ childId: 'child-1', template: taskTemplate({ subtasks: twoSteps }) }],
    };
    const { fixture, compiled } = await setup(state);
    const select = compiled.querySelector<HTMLSelectElement>('#onboardingTaskCalendar')!;

    typeInto(select, 'cal-2');
    await settle(fixture);
    fixture.componentRef.setInput('setup', { ...state, hasScheduledRoutine: true });
    await settle(fixture);

    expect(select.value).toBe('cal-2');
  });

  it('only schedules within the window the guide can find the routine in again', async () => {
    const { fixture, compiled, calendars } = await setup({
      ...base,
      templates: [{ childId: 'child-1', template: taskTemplate({ subtasks: twoSteps }) }],
    });

    const component = fixture.componentInstance as unknown as { date: { set(v: string): void } };
    component.date.set(addDaysIso(todayIsoDate(), 181));
    await settle(fixture);

    expect(compiled.textContent).toContain('Pick a date between today and six months ahead.');
    expect(buttonByText(compiled, 'Add to calendar')!.disabled).toBe(true);
    expect(calendars.scheduleTaskFromTemplate).not.toHaveBeenCalled();
  });

  it('locks the steps a failed attempt already saved', async () => {
    const addSubtask = vi
      .fn()
      .mockResolvedValueOnce(taskTemplate())
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
    const { fixture, compiled } = await setup(base, { taskLibrary: { addSubtask } });

    await fillNewRoutine(fixture);
    saveForm(compiled);
    await settle(fixture);

    const [first, second] = Array.from(
      compiled.querySelectorAll<HTMLInputElement>('input[aria-label^="Step "]'),
    );
    expect(first!.readOnly).toBe(true);
    expect(second!.readOnly).toBe(false);
  });
});
