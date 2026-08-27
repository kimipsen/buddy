import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Subtask, TaskTemplate } from '../../../../core/task-library.service';
import { TaskPicker } from './task-picker';

describe('TaskPicker', () => {
  function subtask(overrides: Partial<Subtask> = {}): Subtask {
    return { id: 'subtask-1', title: 'Brush teeth', icon: '🪥', durationMinutes: 5, ...overrides };
  }

  function template(overrides: Partial<TaskTemplate> = {}): TaskTemplate {
    return {
      id: 'template-1',
      name: 'Get ready for school',
      icon: '🎒',
      color: '#6366f1',
      subtasks: [],
      totalDurationMinutes: 0,
      isArchived: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      ...overrides
    };
  }

  const getReady = template({
    id: 'template-1',
    name: 'Get ready for school',
    icon: '🎒',
    subtasks: [subtask({ id: 's1' }), subtask({ id: 's2', durationMinutes: 10 }), subtask({ id: 's3', durationMinutes: 20 })],
    totalDurationMinutes: 35
  });
  const bedtime = template({ id: 'template-2', name: 'Bedtime routine', icon: '🌙', subtasks: [subtask({ id: 's4', durationMinutes: 90 })], totalDurationMinutes: 90 });
  const chores = template({ id: 'template-3', name: 'Weekend chores', icon: '🧹', subtasks: [], totalDurationMinutes: 0 });

  async function setup(options: { templates?: TaskTemplate[]; templateId?: string; disabled?: boolean } = {}) {
    await TestBed.configureTestingModule({ imports: [TaskPicker] }).compileComponents();

    const fixture = TestBed.createComponent(TaskPicker);
    const onTemplateIdChange = vi.fn();
    fixture.componentInstance.templateIdChange.subscribe(onTemplateIdChange);

    fixture.componentRef.setInput('templates', options.templates ?? [getReady, bedtime, chores]);
    fixture.componentRef.setInput('templateId', options.templateId ?? '');
    fixture.componentRef.setInput('disabled', options.disabled ?? false);
    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement, onTemplateIdChange };
  }

  function textInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input')!;
  }

  function fireEvent(fixture: ComponentFixture<TaskPicker>, event: Event): void {
    textInput(fixture.nativeElement as HTMLElement).dispatchEvent(event);
    fixture.detectChanges();
  }

  function openDropdown(fixture: ComponentFixture<TaskPicker>): void {
    fireEvent(fixture, new Event('focus'));
  }

  function typeQuery(fixture: ComponentFixture<TaskPicker>, value: string): void {
    const input = textInput(fixture.nativeElement as HTMLElement);
    input.value = value;
    fireEvent(fixture, new Event('input'));
  }

  function pressKey(fixture: ComponentFixture<TaskPicker>, key: string): void {
    fireEvent(fixture, new KeyboardEvent('keydown', { key }));
  }

  function optionButtons(compiled: HTMLElement): HTMLButtonElement[] {
    return Array.from(compiled.querySelectorAll('ul li button'));
  }

  function findTemplateOption(compiled: HTMLElement, name: string): HTMLButtonElement {
    return optionButtons(compiled).find((button) => button.textContent?.includes(name))!;
  }

  function clickOption(fixture: ComponentFixture<TaskPicker>, name: string): void {
    const compiled = fixture.nativeElement as HTMLElement;
    findTemplateOption(compiled, name).click();
    fixture.detectChanges();
  }

  describe('closed display value', () => {
    it('shows nothing when no template is selected', async () => {
      const { compiled } = await setup({ templateId: '' });

      expect(textInput(compiled).value).toBe('');
    });

    it("shows the selected template's icon and name", async () => {
      const { compiled } = await setup({ templateId: 'template-2' });

      expect(textInput(compiled).value).toBe('🌙 Bedtime routine');
    });

    it('shows nothing when templateId does not match any template in the list', async () => {
      const { compiled } = await setup({ templateId: 'does-not-exist' });

      expect(textInput(compiled).value).toBe('');
    });

    it('renders the native input as disabled and does not open the dropdown on focus', async () => {
      const { fixture, compiled } = await setup({ disabled: true });

      expect(textInput(compiled).disabled).toBe(true);

      openDropdown(fixture);

      expect(compiled.querySelector('ul')).toBeFalsy();
    });
  });

  describe('opening the dropdown', () => {
    it('renders the "no template" option followed by every template with its icon and name', async () => {
      const { fixture, compiled } = await setup({ templateId: 'template-2' });

      openDropdown(fixture);

      const labels = optionButtons(compiled).map((button) => button.textContent!.replace(/\s+/g, ' ').trim());
      expect(labels[0]).toBe('No template');
      expect(labels[1]).toContain('🎒 Get ready for school');
      expect(labels[2]).toContain('🌙 Bedtime routine');
      expect(labels[3]).toContain('🧹 Weekend chores');
    });

    it("shows each template's subtask count and total duration alongside its name", async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);

      expect(findTemplateOption(compiled, 'Get ready for school').textContent).toContain('3 steps, 35m');
      expect(findTemplateOption(compiled, 'Bedtime routine').textContent).toContain('1 steps, 1h 30m');
      expect(findTemplateOption(compiled, 'Weekend chores').textContent).toContain('0 steps, 0m');
    });

    it('clears the visible text so the input starts blank even when a template was already selected', async () => {
      const { fixture, compiled } = await setup({ templateId: 'template-2' });

      openDropdown(fixture);

      expect(textInput(compiled).value).toBe('');
    });
  });

  describe('filtering', () => {
    it('filters the option list to templates whose name contains the query, case-insensitively', async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);
      typeQuery(fixture, 'BED');

      const labels = optionButtons(compiled).map((button) => button.textContent!.trim());
      expect(labels).toHaveLength(2);
      expect(labels[1]).toContain('Bedtime routine');
    });

    it('shows a "no matches" message and no template options when nothing matches', async () => {
      const { fixture, compiled } = await setup();

      openDropdown(fixture);
      typeQuery(fixture, 'pizza');

      expect(optionButtons(compiled)).toHaveLength(1);
      expect(compiled.textContent).toContain('No templates match.');
    });
  });

  describe('selecting a template', () => {
    it('emits the id of the clicked template', async () => {
      const { fixture, onTemplateIdChange } = await setup({ templateId: '' });

      openDropdown(fixture);
      clickOption(fixture, 'Bedtime routine');

      expect(onTemplateIdChange).toHaveBeenCalledExactlyOnceWith('template-2');
    });

    it('closes the dropdown after selecting a template', async () => {
      const { fixture, compiled } = await setup({ templateId: '' });

      openDropdown(fixture);
      clickOption(fixture, 'Bedtime routine');

      expect(compiled.querySelector('ul')).toBeFalsy();
    });

    it('emits an empty string when choosing "no template"', async () => {
      const { fixture, onTemplateIdChange } = await setup({ templateId: 'template-1' });

      openDropdown(fixture);
      clickOption(fixture, 'No template');

      expect(onTemplateIdChange).toHaveBeenCalledExactlyOnceWith('');
    });

    it('does not emit when re-selecting the already-selected template', async () => {
      const { fixture, onTemplateIdChange } = await setup({ templateId: 'template-2' });

      openDropdown(fixture);
      clickOption(fixture, 'Bedtime routine');

      expect(onTemplateIdChange).not.toHaveBeenCalled();
    });
  });

  describe('keyboard interaction', () => {
    it('Escape closes the dropdown without emitting', async () => {
      const { fixture, compiled, onTemplateIdChange } = await setup({ templateId: '' });

      openDropdown(fixture);
      pressKey(fixture, 'Escape');

      expect(compiled.querySelector('ul')).toBeFalsy();
      expect(onTemplateIdChange).not.toHaveBeenCalled();
    });

    it('Enter selects the first template in the (unfiltered) list', async () => {
      const { fixture, onTemplateIdChange } = await setup({ templateId: '' });

      openDropdown(fixture);
      pressKey(fixture, 'Enter');

      expect(onTemplateIdChange).toHaveBeenCalledExactlyOnceWith('template-1');
    });

    it('Enter does nothing when the filter matches no template', async () => {
      const { fixture, onTemplateIdChange } = await setup({ templateId: '' });

      openDropdown(fixture);
      typeQuery(fixture, 'pizza');
      pressKey(fixture, 'Enter');

      expect(onTemplateIdChange).not.toHaveBeenCalled();
    });
  });

  describe('clicking outside', () => {
    it('closes the dropdown when clicking the backdrop overlay', async () => {
      const { fixture, compiled } = await setup({ templateId: '' });

      openDropdown(fixture);
      const backdrop = compiled.querySelector('.fixed.inset-0') as HTMLElement;
      expect(backdrop).toBeTruthy();

      backdrop.click();
      fixture.detectChanges();

      expect(compiled.querySelector('ul')).toBeFalsy();
    });
  });
});
