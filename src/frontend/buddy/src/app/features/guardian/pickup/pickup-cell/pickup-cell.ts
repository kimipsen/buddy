import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ChildSummary, GuardianSummary } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { AssignPickupRequest, PickupAssigneeKind, PickupOccurrence } from '../../../../core/pickups.service';
import { TimeOfDayPipe } from '../../../../core/time-of-day.pipe';
import { TimeSelect } from '../../../../shared/time-select/time-select';

const GUARDIAN: PickupAssigneeKind = 0;
const SELF_ESCORT: PickupAssigneeKind = 1;
const SIBLING: PickupAssigneeKind = 2;
const PLAYDATE: PickupAssigneeKind = 3;

// The weekly grid renders many `app-pickup-cell` instances at once, so form-control ids need a
// per-instance suffix to stay unique across all of them (see `instanceId` below).
let nextPickupCellInstanceId = 0;

// One weekly-grid cell: displays the current assignment (if any) as a compact summary, or an
// inline edit form when clicked -- no modal exists anywhere in this codebase (see
// manage-medicines.ts's inline confirm/cancel pattern), so editing happens in place the same way.
@Component({
  selector: 'app-pickup-cell',
  imports: [FormsModule, TranslatePipe, TimeOfDayPipe, TimeSelect],
  templateUrl: './pickup-cell.html'
})
export class PickupCell {
  protected readonly instanceId = `pickup-cell-${nextPickupCellInstanceId++}`;

  readonly guardians = input.required<GuardianSummary[]>();
  readonly siblings = input.required<ChildSummary[]>();
  readonly occurrence = input<PickupOccurrence | null>(null);
  readonly disabled = input(false);
  readonly saving = input(false);

  readonly assign = output<AssignPickupRequest>();
  readonly clear = output<void>();

  protected readonly guardianKind = GUARDIAN;
  protected readonly selfEscortKind = SELF_ESCORT;
  protected readonly siblingKind = SIBLING;
  protected readonly playdateKind = PLAYDATE;

  protected readonly editing = signal(false);
  protected readonly kind = signal<PickupAssigneeKind>(GUARDIAN);
  protected readonly guardianId = signal('');
  protected readonly siblingChildId = signal('');
  protected readonly playdateHostName = signal('');
  protected readonly playdateLocation = signal('');
  protected readonly playdateContactInfo = signal('');
  protected readonly time = signal('');
  protected readonly notes = signal('');

  protected readonly canSave = computed(() => {
    switch (this.kind()) {
      case GUARDIAN:
        return !!this.guardianId();
      case SIBLING:
        return !!this.siblingChildId();
      case PLAYDATE:
        return !!this.playdateHostName().trim();
      default:
        return true;
    }
  });

  protected readonly summaryGuardianName = computed(() => {
    const occurrence = this.occurrence();
    return this.guardians().find((guardian) => guardian.id === occurrence?.guardianId)?.name.givenName ?? null;
  });

  protected readonly summarySiblingName = computed(() => {
    const occurrence = this.occurrence();
    return this.siblings().find((sibling) => sibling.id === occurrence?.siblingChildId)?.name.givenName ?? null;
  });

  protected startEditing(): void {
    if (this.disabled()) {
      return;
    }

    const occurrence = this.occurrence();

    this.kind.set(occurrence?.kind ?? GUARDIAN);
    this.guardianId.set(occurrence?.guardianId ?? '');
    this.siblingChildId.set(occurrence?.siblingChildId ?? '');
    this.playdateHostName.set(occurrence?.playdateHostName ?? '');
    this.playdateLocation.set(occurrence?.playdateLocation ?? '');
    this.playdateContactInfo.set(occurrence?.playdateContactInfo ?? '');
    this.time.set(occurrence?.time?.slice(0, 5) ?? '');
    this.notes.set(occurrence?.notes ?? '');
    this.editing.set(true);
  }

  protected cancelEditing(): void {
    this.editing.set(false);
  }

  protected save(): void {
    if (!this.canSave()) {
      return;
    }

    this.assign.emit({
      kind: this.kind(),
      guardianId: this.kind() === GUARDIAN ? this.guardianId() : null,
      siblingChildId: this.kind() === SIBLING ? this.siblingChildId() : null,
      playdateHostName: this.kind() === PLAYDATE ? this.playdateHostName().trim() : null,
      playdateLocation: this.kind() === PLAYDATE ? this.playdateLocation().trim() || null : null,
      playdateContactInfo: this.kind() === PLAYDATE ? this.playdateContactInfo().trim() || null : null,
      time: this.time() ? `${this.time()}:00` : null,
      notes: this.notes().trim() || null
    });
    this.editing.set(false);
  }

  protected clearAssignment(): void {
    this.editing.set(false);
    this.clear.emit();
  }
}
