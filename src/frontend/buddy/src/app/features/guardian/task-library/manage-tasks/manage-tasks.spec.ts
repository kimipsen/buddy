import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { Subtask, TaskLibraryService, TaskTemplate, TaskTemplateDetails } from '../../../../core/task-library.service';
import { ManageTasks } from './manage-tasks';

describe('ManageTasks', () => {
  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return { id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' }, guardianLinkId: 'link-1', kind: 0, language: 'en', timeZoneId: 'UTC', ...overrides };
  }

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

  interface SetupOptions {
    initialTemplates?: TaskTemplate[];
    guardians?: Partial<GuardiansService>;
    taskLibrary?: Partial<TaskLibraryService>;
  }

  // Mirrors TaskLibraryService's real signal-mutation semantics (listTaskTemplates replaces,
  // createTaskTemplate appends, archiveTaskTemplate marks-not-removes, subtask mutations replace
  // the owning template) so the component -- which reads `taskLibrary.templates()` directly --
  // behaves under test the same way it does against the real service. Same approach as
  // manage-meals.spec.ts's mealplansStub.
  async function setup(options: SetupOptions = {}) {
    const templatesState: WritableSignal<TaskTemplate[]> = signal(options.initialTemplates ?? []);

    function replace(updated: TaskTemplate): TaskTemplate {
      templatesState.update((current) => current.map((existing) => (existing.id === updated.id ? updated : existing)));
      return updated;
    }

    const taskLibraryStub: Partial<TaskLibraryService> = {
      templates: templatesState.asReadonly(),
      listTaskTemplates: vi.fn(async () => templatesState()),
      createTaskTemplate: vi.fn(async (_childId: string, request: TaskTemplateDetails) => {
        const created = template({ id: `template-created-${templatesState().length + 1}`, ...request });
        templatesState.update((current) => [...current, created]);
        return created;
      }),
      updateTaskTemplate: vi.fn(async (templateId: string, request: TaskTemplateDetails) => {
        return replace({ ...templatesState().find((t) => t.id === templateId)!, ...request });
      }),
      archiveTaskTemplate: vi.fn(async (templateId: string) => {
        templatesState.update((current) => current.map((t) => (t.id === templateId ? { ...t, isArchived: true } : t)));
      }),
      addSubtask: vi.fn(async (templateId: string, title: string, icon: string | null, durationMinutes: number) => {
        const current = templatesState().find((t) => t.id === templateId)!;
        const added = subtask({ id: `subtask-created-${current.subtasks.length + 1}`, title, icon, durationMinutes });
        const subtasks = [...current.subtasks, added];
        return replace({ ...current, subtasks, totalDurationMinutes: subtasks.reduce((sum, s) => sum + s.durationMinutes, 0) });
      }),
      updateSubtask: vi.fn(async (templateId: string, subtaskId: string, title: string, icon: string | null, durationMinutes: number) => {
        const current = templatesState().find((t) => t.id === templateId)!;
        const subtasks = current.subtasks.map((s) => (s.id === subtaskId ? { ...s, title, icon, durationMinutes } : s));
        return replace({ ...current, subtasks, totalDurationMinutes: subtasks.reduce((sum, s) => sum + s.durationMinutes, 0) });
      }),
      removeSubtask: vi.fn(async (templateId: string, subtaskId: string) => {
        const current = templatesState().find((t) => t.id === templateId)!;
        const subtasks = current.subtasks.filter((s) => s.id !== subtaskId);
        replace({ ...current, subtasks, totalDurationMinutes: subtasks.reduce((sum, s) => sum + s.durationMinutes, 0) });
      }),
      reorderSubtasks: vi.fn(async (templateId: string, newOrder: string[]) => {
        const current = templatesState().find((t) => t.id === templateId)!;
        const byId = new Map(current.subtasks.map((s) => [s.id, s]));
        const subtasks = newOrder.map((id) => byId.get(id)!);
        return replace({ ...current, subtasks });
      }),
      ...options.taskLibrary
    };

    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      ...options.guardians
    };

    await TestBed.configureTestingModule({
      imports: [ManageTasks],
      providers: [
        { provide: TaskLibraryService, useValue: taskLibraryStub },
        { provide: GuardiansService, useValue: guardiansStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageTasks);

    return { fixture, taskLibrary: taskLibraryStub, guardians: guardiansStub, templatesState };
  }

  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  function findAllButtonsByText(compiled: HTMLElement, text: string): HTMLButtonElement[] {
    return Array.from(compiled.querySelectorAll('button')).filter((button) => button.textContent?.trim() === text);
  }

  function setInputValue(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function templateNameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="templateName"]')!;
  }

  function templateIconInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="templateIcon"]')!;
  }

  function subtaskTitleInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="newSubtaskTitle"]')!;
  }

  function subtaskDurationInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector('input[name="newSubtaskDuration"]')!;
  }

  function selectsOutsideForm(compiled: HTMLElement): HTMLSelectElement[] {
    return Array.from(compiled.querySelectorAll<HTMLSelectElement>('select')).filter((select) => !select.closest('form'));
  }

  describe('loading / empty / error states', () => {
    it('shows the loading message before children resolve', async () => {
      const { fixture } = await setup();
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading task templates');
    });

    it('shows the no-children message when the guardian has no linked children', async () => {
      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Link a child from Settings before building a task library.');
      expect(compiled.querySelector('form')).toBeFalsy();
    });

    it('shows a translated error when loading children fails', async () => {
      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Unable to load children.');
    });

    it('shows the empty state once loading finishes with no templates', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('No task templates yet. Add one below.');
    });
  });

  describe('child selection', () => {
    it('does not show a child selector when there is only one child', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      expect(selectsOutsideForm(fixture.nativeElement as HTMLElement)).toHaveLength(0);
    });

    it('loads the first child automatically and requests its templates', async () => {
      const listTaskTemplates = vi.fn(async () => []);
      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-9' })]) }, taskLibrary: { listTaskTemplates } });
      await settle(fixture);

      expect(listTaskTemplates).toHaveBeenCalledWith('child-9');
    });

    it('shows a selector for multiple children and reloads templates when the selection changes', async () => {
      const childA = child({ id: 'child-a' });
      const childB = child({ id: 'child-b' });
      const listTaskTemplates = vi.fn(async () => []);

      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [childA, childB]) }, taskLibrary: { listTaskTemplates } });
      await settle(fixture);

      const select = selectsOutsideForm(fixture.nativeElement as HTMLElement)[0];
      select.value = 'child-b';
      select.dispatchEvent(new Event('change'));
      await settle(fixture);

      expect(listTaskTemplates).toHaveBeenLastCalledWith('child-b');
    });
  });

  describe('template rendering', () => {
    it('renders each template from the shared service signal, with its icon, name, and total duration', async () => {
      const templates = [
        template({ id: 'template-1', name: 'Get ready', icon: '🎒', totalDurationMinutes: 35 }),
        template({ id: 'template-2', name: 'Bedtime', icon: '🌙', totalDurationMinutes: 90 })
      ];
      const { fixture } = await setup({ initialTemplates: templates });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Get ready');
      expect(compiled.textContent).toContain('🎒');
      expect(compiled.textContent).toContain('35m total');
      expect(compiled.textContent).toContain('Bedtime');
      expect(compiled.textContent).toContain('1h 30m total');
    });

    it('shows an archived badge for archived templates but keeps them visible rather than filtering them out', async () => {
      const templates = [template({ id: 'template-1', name: 'Active Template', isArchived: false }), template({ id: 'template-2', name: 'Old Template', isArchived: true })];
      const { fixture } = await setup({ initialTemplates: templates });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Active Template');
      expect(compiled.textContent).toContain('Old Template');
      expect(compiled.textContent).toContain('Archived');
    });

    it('hides the archive button for an already-archived template', async () => {
      const { fixture } = await setup({ initialTemplates: [template({ isArchived: true })] });
      await settle(fixture);

      expect(findButtonByText(fixture.nativeElement as HTMLElement, 'Archive')).toBeUndefined();
    });
  });

  describe('creating a template', () => {
    it('disables the submit button until a name is entered', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const submit = findButtonByText(compiled, 'Add template')!;
      expect(submit.disabled).toBe(true);

      setInputValue(templateNameInput(compiled), 'Bedtime routine');
      fixture.detectChanges();

      expect(submit.disabled).toBe(false);
    });

    it('submits trimmed field values and expands the newly created template', async () => {
      const { fixture, taskLibrary } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(templateNameInput(compiled), '  Bedtime routine  ');
      setInputValue(templateIconInput(compiled), ' 🌙 ');
      fixture.detectChanges();
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(taskLibrary.createTaskTemplate).toHaveBeenCalledWith('child-1', expect.objectContaining({ name: 'Bedtime routine', icon: '🌙' }));
      expect(compiled.textContent).toContain('Bedtime routine');
      // Expanded automatically -- the subtasks panel (with its own add-subtask form) is visible.
      expect(compiled.textContent).toContain('Subtasks');
    });

    it('resets the form after a successful create', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(templateNameInput(compiled), 'Bedtime routine');
      fixture.detectChanges();
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(templateNameInput(compiled).value).toBe('');
    });

    it('shows a translated error and keeps the entered name when creation fails', async () => {
      const { fixture } = await setup({ taskLibrary: { createTaskTemplate: vi.fn(async () => Promise.reject(new Error('boom'))) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      setInputValue(templateNameInput(compiled), 'Bedtime routine');
      fixture.detectChanges();
      compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to create the task template.');
      expect(templateNameInput(compiled).value).toBe('Bedtime routine');
    });
  });

  describe('renaming a template', () => {
    it('renames the overall task template inline and saves the change', async () => {
      const { fixture, taskLibrary } = await setup({ initialTemplates: [template({ id: 'template-1', name: 'Get ready', icon: '🎒', color: '#6366f1' })] });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Rename')!.click();
      fixture.detectChanges();

      const nameInput = compiled.querySelector<HTMLInputElement>('input[name="editTemplateName"]')!;
      setInputValue(nameInput, 'Morning routine');
      fixture.detectChanges();

      findButtonByText(compiled, 'Save')!.click();
      await settle(fixture);

      expect(taskLibrary.updateTaskTemplate).toHaveBeenCalledWith('template-1', { name: 'Morning routine', icon: '🎒', color: '#6366f1' });
      expect(compiled.textContent).toContain('Morning routine');
      expect(compiled.textContent).not.toContain('Get ready');
    });

    it('cancels an in-progress rename without saving', async () => {
      const { fixture, taskLibrary } = await setup({ initialTemplates: [template({ id: 'template-1', name: 'Get ready' })] });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Rename')!.click();
      fixture.detectChanges();
      setInputValue(compiled.querySelector<HTMLInputElement>('input[name="editTemplateName"]')!, 'Something else');
      fixture.detectChanges();

      findButtonByText(compiled, 'Cancel')!.click();
      fixture.detectChanges();

      expect(taskLibrary.updateTaskTemplate).not.toHaveBeenCalled();
      expect(compiled.textContent).toContain('Get ready');
    });

    it('shows a translated error and keeps the edit form open when renaming fails', async () => {
      const { fixture } = await setup({
        initialTemplates: [template({ id: 'template-1', name: 'Get ready' })],
        taskLibrary: { updateTaskTemplate: vi.fn(async () => Promise.reject(new Error('boom'))) }
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Rename')!.click();
      fixture.detectChanges();
      setInputValue(compiled.querySelector<HTMLInputElement>('input[name="editTemplateName"]')!, 'Morning routine');
      fixture.detectChanges();
      findButtonByText(compiled, 'Save')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to update this task template.');
      expect(compiled.querySelector<HTMLInputElement>('input[name="editTemplateName"]')).not.toBeNull();
    });

    it('hides the rename button for an already-archived template', async () => {
      const { fixture } = await setup({ initialTemplates: [template({ isArchived: true })] });
      await settle(fixture);

      expect(findButtonByText(fixture.nativeElement as HTMLElement, 'Rename')).toBeUndefined();
    });
  });

  describe('archiving a template', () => {
    it('archives the clicked template and marks it archived without removing it', async () => {
      const { fixture, taskLibrary } = await setup({ initialTemplates: [template({ id: 'template-1', name: 'Get ready' })] });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Archive')!.click();
      await settle(fixture);

      expect(taskLibrary.archiveTaskTemplate).toHaveBeenCalledWith('template-1');
      expect(compiled.textContent).toContain('Get ready');
      expect(compiled.textContent).toContain('Archived');
    });

    it('shows a translated error when archiving fails', async () => {
      const { fixture } = await setup({
        initialTemplates: [template()],
        taskLibrary: { archiveTaskTemplate: vi.fn(async () => Promise.reject(new Error('boom'))) }
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Archive')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to archive this task template.');
    });
  });

  describe('subtasks', () => {
    async function setupExpanded(templates: TaskTemplate[] = [template({ subtasks: [subtask()] })]) {
      const result = await setup({ initialTemplates: templates });
      await settle(result.fixture);

      const compiled = result.fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Edit subtasks')!.click();
      await settle(result.fixture);

      return result;
    }

    it('toggles the subtasks panel open and closed', async () => {
      const { fixture } = await setupExpanded();
      const compiled = fixture.nativeElement as HTMLElement;

      expect(compiled.textContent).toContain('Brush teeth');

      findButtonByText(compiled, 'Hide subtasks')!.click();
      await settle(fixture);

      expect(compiled.textContent).not.toContain('Brush teeth');
    });

    it('adds a subtask via the inline form and clears it afterwards', async () => {
      const { fixture, taskLibrary } = await setupExpanded([template({ id: 'template-1', subtasks: [] })]);
      const compiled = fixture.nativeElement as HTMLElement;

      setInputValue(subtaskTitleInput(compiled), 'Pack lunch');
      setInputValue(subtaskDurationInput(compiled), '10');
      fixture.detectChanges();

      findButtonByText(compiled, 'Add subtask')!.click();
      await settle(fixture);

      expect(taskLibrary.addSubtask).toHaveBeenCalledWith('template-1', 'Pack lunch', null, 10);
      expect(compiled.textContent).toContain('Pack lunch');
      expect(subtaskTitleInput(compiled).value).toBe('');
    });

    it('disables the add-subtask button until a title is entered', async () => {
      const { fixture } = await setupExpanded([template({ id: 'template-1', subtasks: [] })]);
      const compiled = fixture.nativeElement as HTMLElement;

      expect(findButtonByText(compiled, 'Add subtask')!.disabled).toBe(true);

      setInputValue(subtaskTitleInput(compiled), 'Pack lunch');
      fixture.detectChanges();

      expect(findButtonByText(compiled, 'Add subtask')!.disabled).toBe(false);
    });

    it('shows a translated error when adding a subtask fails', async () => {
      const { fixture, taskLibrary } = await setupExpanded([template({ id: 'template-1', subtasks: [] })]);
      (taskLibrary.addSubtask as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('boom'));
      const compiled = fixture.nativeElement as HTMLElement;

      setInputValue(subtaskTitleInput(compiled), 'Pack lunch');
      fixture.detectChanges();
      findButtonByText(compiled, 'Add subtask')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to add this subtask.');
    });

    it('edits a subtask inline and saves the change', async () => {
      const { fixture, taskLibrary } = await setupExpanded();
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Edit')!.click();
      fixture.detectChanges();

      const titleInput = compiled.querySelector<HTMLInputElement>('input[name="editSubtaskTitle-subtask-1"]')!;
      setInputValue(titleInput, 'Brush teeth well');
      fixture.detectChanges();

      findButtonByText(compiled, 'Save')!.click();
      await settle(fixture);

      expect(taskLibrary.updateSubtask).toHaveBeenCalledWith('template-1', 'subtask-1', 'Brush teeth well', '🪥', 5);
      expect(compiled.textContent).toContain('Brush teeth well');
    });

    it('cancels an in-progress subtask edit without saving', async () => {
      const { fixture, taskLibrary } = await setupExpanded();
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Edit')!.click();
      fixture.detectChanges();
      findButtonByText(compiled, 'Cancel')!.click();
      fixture.detectChanges();

      expect(taskLibrary.updateSubtask).not.toHaveBeenCalled();
      expect(compiled.textContent).toContain('Brush teeth');
    });

    it('removes a subtask', async () => {
      const { fixture, taskLibrary } = await setupExpanded();
      const compiled = fixture.nativeElement as HTMLElement;

      findButtonByText(compiled, 'Remove')!.click();
      await settle(fixture);

      expect(taskLibrary.removeSubtask).toHaveBeenCalledWith('template-1', 'subtask-1');
      expect(compiled.textContent).not.toContain('Brush teeth');
    });

    it('moves a subtask down and up via the reorder buttons, submitting the full recomputed order', async () => {
      const templates = [
        template({
          id: 'template-1',
          subtasks: [subtask({ id: 'subtask-1', title: 'First' }), subtask({ id: 'subtask-2', title: 'Second' })]
        })
      ];
      const { fixture, taskLibrary } = await setupExpanded(templates);
      const compiled = fixture.nativeElement as HTMLElement;

      const moveDownButtons = () => Array.from(compiled.querySelectorAll('button[aria-label="Move down"]')) as HTMLButtonElement[];
      moveDownButtons()[0].click();
      await settle(fixture);

      expect(taskLibrary.reorderSubtasks).toHaveBeenCalledWith('template-1', ['subtask-2', 'subtask-1']);
    });

    it('disables the up-arrow for the first subtask and the down-arrow for the last', async () => {
      const templates = [
        template({
          id: 'template-1',
          subtasks: [subtask({ id: 'subtask-1', title: 'First' }), subtask({ id: 'subtask-2', title: 'Second' })]
        })
      ];
      const { fixture } = await setupExpanded(templates);
      const compiled = fixture.nativeElement as HTMLElement;

      const upButtons = Array.from(compiled.querySelectorAll('button[aria-label="Move up"]')) as HTMLButtonElement[];
      const downButtons = Array.from(compiled.querySelectorAll('button[aria-label="Move down"]')) as HTMLButtonElement[];

      expect(upButtons[0].disabled).toBe(true);
      expect(downButtons[0].disabled).toBe(false);
      expect(upButtons[1].disabled).toBe(false);
      expect(downButtons[1].disabled).toBe(true);
    });
  });
});
