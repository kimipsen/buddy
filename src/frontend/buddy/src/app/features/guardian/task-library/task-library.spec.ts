import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GuardiansService } from '../../../core/guardians.service';
import { TaskLibraryService, TaskTemplate } from '../../../core/task-library.service';
import { GuardianTaskLibrary } from './task-library';

// GuardianTaskLibrary is a trivial shell: a back link plus <app-manage-tasks>, no logic of its
// own -- mirrors GuardianMedicine's own smoke test. ManageTasks' own behavior is covered by
// manage-tasks.spec.ts; its child service still needs a stub here since mounting the real
// ManageTasks otherwise instantiates the real (HttpClient-backed) TaskLibraryService via DI.
describe('GuardianTaskLibrary', () => {
  async function setup() {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [])
    };
    const taskLibraryStub: Partial<TaskLibraryService> = {
      templates: signal<TaskTemplate[]>([]).asReadonly(),
      listTaskTemplates: vi.fn(async () => [])
    };

    await TestBed.configureTestingModule({
      imports: [GuardianTaskLibrary],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: TaskLibraryService, useValue: taskLibraryStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianTaskLibrary);

    return { fixture };
  }

  it('renders the manage-tasks panel and a back link to the guardian home', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-manage-tasks')).toBeTruthy();
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();
  });
});
