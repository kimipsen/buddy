import { Component, ElementRef, computed, inject, input, output, signal } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TaskTemplate } from '../../../../core/task-library.service';

// Formats a whole-minutes duration for display (e.g. "35m", "1h", "1h 30m") -- same convention as
// ManageTasks's own formatDuration, duplicated rather than shared since neither component imports
// from the other and this is the only place besides ManageTasks that needs it.
function formatDuration(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours === 0) {
    return `${minutes}m`;
  }

  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`;
}

// Closely mirrors MealPicker's searchable-dropdown contract (templates/templateId/disabled
// inputs, templateIdChange output, open/query/dropdownStyle signal state, fixed-position
// dropdown positioned off the trigger's bounding rect) -- see meal-picker.ts. Additionally shows
// each result's subtask count and total duration, since unlike a Meal a TaskTemplate has a
// nested, sizeable subtask collection worth surfacing before picking it.
@Component({
  selector: 'app-task-picker',
  imports: [TranslatePipe],
  templateUrl: './task-picker.html'
})
export class TaskPicker {
  readonly templates = input.required<TaskTemplate[]>();
  readonly templateId = input('');
  readonly disabled = input(false);

  readonly templateIdChange = output<string>();

  private readonly elementRef = inject(ElementRef<HTMLElement>);

  protected readonly open = signal(false);
  protected readonly query = signal('');
  protected readonly dropdownStyle = signal<Record<string, string>>({});

  protected readonly selectedTemplate = computed(() => this.templates().find((template) => template.id === this.templateId()) ?? null);

  protected readonly displayValue = computed(() => {
    if (this.open()) {
      return this.query();
    }

    const template = this.selectedTemplate();
    return template ? `${template.icon} ${template.name}` : '';
  });

  protected readonly filteredTemplates = computed(() => {
    const query = this.query().trim().toLowerCase();

    if (!query) {
      return this.templates();
    }

    return this.templates().filter((template) => template.name.toLowerCase().includes(query));
  });

  protected openDropdown(): void {
    if (this.disabled()) {
      return;
    }

    this.query.set('');
    this.open.set(true);

    const rect = this.elementRef.nativeElement.getBoundingClientRect();
    this.dropdownStyle.set({
      position: 'fixed',
      top: `${rect.bottom + 4}px`,
      left: `${rect.left}px`,
      width: `${rect.width}px`
    });
  }

  protected closeDropdown(): void {
    this.open.set(false);
    this.query.set('');
  }

  protected onQueryInput(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected selectTemplate(template: TaskTemplate | null): void {
    this.closeDropdown();

    if ((template?.id ?? '') !== this.templateId()) {
      this.templateIdChange.emit(template?.id ?? '');
    }
  }

  protected selectFirstMatch(): void {
    const [first] = this.filteredTemplates();

    if (first) {
      this.selectTemplate(first);
    }
  }

  protected summary(template: TaskTemplate): string {
    return formatDuration(template.totalDurationMinutes);
  }
}
