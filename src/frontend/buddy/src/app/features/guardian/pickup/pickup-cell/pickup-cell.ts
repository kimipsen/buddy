import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { FeaturesService } from '../../../../core/features.service';
import { ChildBabysitter } from '../../../../core/babysitters.service';
import { ChildSummary, GuardianSummary } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  AssignPickupRequest,
  PickupAssignee,
  PickupAssigneeKind,
  PickupOccurrence,
} from '../../../../core/pickups.service';
import { TimeOfDayPipe } from '../../../../core/time-of-day.pipe';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { TimeSelect } from '../../../../shared/time-select/time-select';

const GUARDIAN = 0 satisfies PickupAssigneeKind;
const SELF_ESCORT = 1 satisfies PickupAssigneeKind;
const SIBLING = 2 satisfies PickupAssigneeKind;
const PLAYDATE = 3 satisfies PickupAssigneeKind;
const BABYSITTER = 4 satisfies PickupAssigneeKind;

// A babysitter is picked by the (owner guardian, babysitter) pair; the select needs one string.
function babysitterKey(guardianId: string, babysitterId: string): string {
  return `${guardianId}|${babysitterId}`;
}

// The weekly grid renders many `app-pickup-cell` instances at once, so form-control ids need a
// per-instance suffix to stay unique across all of them (see `instanceId` below).
let nextPickupCellInstanceId = 0;

// One weekly-grid cell: displays the current assignment (if any) as a compact summary, or an
// inline edit form when clicked -- no modal exists anywhere in this codebase (see
// manage-medicines.ts's inline confirm/cancel pattern), so editing happens in place the same way.
@Component({
  selector: 'app-pickup-cell',
  imports: [FormsModule, RouterLink, TranslatePipe, TimeOfDayPipe, SegmentedControl, TimeSelect],
  templateUrl: './pickup-cell.html',
})
export class PickupCell {
  protected readonly features = inject(FeaturesService);

  private readonly translation = inject(TranslationService);

  protected readonly instanceId = `pickup-cell-${nextPickupCellInstanceId++}`;

  protected readonly kindOptions = computed<SegmentedControlOption<PickupAssigneeKind>[]>(() =>
    (
      [
        { value: GUARDIAN, label: this.translation.translate('pickup.cell.kind.guardian') },
        { value: SELF_ESCORT, label: this.translation.translate('pickup.cell.kind.selfEscort') },
        { value: SIBLING, label: this.translation.translate('pickup.cell.kind.sibling') },
        { value: PLAYDATE, label: this.translation.translate('pickup.cell.kind.playdate') },
        { value: BABYSITTER, label: this.translation.translate('pickup.cell.kind.babysitter') },
      ] satisfies SegmentedControlOption<PickupAssigneeKind>[]
    ).filter(
      // With babysitters off, only a slot already assigned to one keeps the option, so it shows.
      (option) =>
        option.value !== BABYSITTER ||
        this.features.enabled('babysitters') ||
        this.kind() === BABYSITTER,
    ),
  );

  readonly guardians = input.required<GuardianSummary[]>();
  readonly siblings = input.required<ChildSummary[]>();
  readonly babysitters = input<ChildBabysitter[]>([]);
  readonly occurrence = input<PickupOccurrence | null>(null);
  readonly disabled = input(false);
  readonly saving = input(false);

  readonly assign = output<AssignPickupRequest>();
  readonly clear = output<void>();

  protected readonly guardianKind = GUARDIAN;
  protected readonly selfEscortKind = SELF_ESCORT;
  protected readonly siblingKind = SIBLING;
  protected readonly playdateKind = PLAYDATE;
  protected readonly babysitterKind = BABYSITTER;
  protected readonly babysitterKey = babysitterKey;

  protected readonly editing = signal(false);
  protected readonly kind = signal<PickupAssigneeKind>(GUARDIAN);
  protected readonly guardianId = signal('');
  protected readonly siblingChildId = signal('');
  protected readonly playdateHostName = signal('');
  protected readonly playdateLocation = signal('');
  protected readonly playdateContactInfo = signal('');
  protected readonly babysitterChoice = signal('');
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
      case BABYSITTER:
        return !!this.chosenBabysitter();
      default:
        return true;
    }
  });

  protected readonly summaryGuardianName = computed(() => {
    const assignee = this.occurrence()?.assignee;
    if (assignee?.kind !== GUARDIAN) {
      return null;
    }
    return (
      this.guardians().find((guardian) => guardian.id === assignee.guardianId)?.name.givenName ??
      null
    );
  });

  protected readonly summarySiblingName = computed(() => {
    const assignee = this.occurrence()?.assignee;
    if (assignee?.kind !== SIBLING) {
      return null;
    }
    return (
      this.siblings().find((sibling) => sibling.id === assignee.siblingChildId)?.name.givenName ??
      null
    );
  });

  // With babysitters off the list isn't loaded, so a slot already assigned to one offers just that
  // babysitter: the guardian can still change its time and notes without reassigning it.
  protected readonly babysitterOptions = computed<ChildBabysitter[]>(() => {
    if (this.features.enabled('babysitters')) {
      return this.babysitters();
    }
    const assignee = this.occurrence()?.assignee;
    return assignee?.kind === BABYSITTER
      ? [
          {
            guardianId: assignee.guardianId,
            id: assignee.babysitterId,
            name: assignee.name ?? '',
            contactInfo: '',
          },
        ]
      : [];
  });

  // Undefined until one is chosen, and for an archived babysitter that left the list.
  private readonly chosenBabysitter = computed(() =>
    this.babysitterOptions().find(
      (b) => babysitterKey(b.guardianId, b.id) === this.babysitterChoice(),
    ),
  );

  // The slot points at a babysitter who has left the list (archived, or their guardian unlinked):
  // the backend refuses to re-save it, so Save stays disabled and the cell says why.
  protected readonly babysitterRemoved = computed(
    () => !!this.babysitterChoice() && !this.chosenBabysitter(),
  );

  protected readonly summaryPlaydateHost = computed(() => {
    const assignee = this.occurrence()?.assignee;
    return assignee?.kind === PLAYDATE ? assignee.hostName : '';
  });

  protected readonly summaryBabysitterName = computed(() => {
    const assignee = this.occurrence()?.assignee;
    return assignee?.kind === BABYSITTER ? assignee.name : '';
  });

  protected startEditing(): void {
    if (this.disabled()) {
      return;
    }

    const occurrence = this.occurrence();
    const assignee = occurrence?.assignee;

    this.kind.set(assignee?.kind ?? GUARDIAN);
    this.guardianId.set(assignee?.kind === GUARDIAN ? assignee.guardianId : '');
    this.siblingChildId.set(assignee?.kind === SIBLING ? assignee.siblingChildId : '');
    this.playdateHostName.set(assignee?.kind === PLAYDATE ? assignee.hostName : '');
    this.playdateLocation.set(assignee?.kind === PLAYDATE ? (assignee.location ?? '') : '');
    this.playdateContactInfo.set(assignee?.kind === PLAYDATE ? (assignee.contactInfo ?? '') : '');
    this.babysitterChoice.set(
      assignee?.kind === BABYSITTER
        ? babysitterKey(assignee.guardianId, assignee.babysitterId)
        : '',
    );
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
      assignee: this.draftAssignee(),
      time: this.time() ? `${this.time()}:00` : null,
      notes: this.notes().trim(),
    });
    this.editing.set(false);
  }

  // Only the selected kind's fields are sent -- the others stay in the form but never reach the API.
  private draftAssignee(): PickupAssignee {
    switch (this.kind()) {
      case GUARDIAN:
        return { kind: GUARDIAN, guardianId: this.guardianId() };
      case SELF_ESCORT:
        return { kind: SELF_ESCORT };
      case SIBLING:
        return { kind: SIBLING, siblingChildId: this.siblingChildId() };
      case PLAYDATE:
        return {
          kind: PLAYDATE,
          hostName: this.playdateHostName().trim(),
          location: this.playdateLocation().trim(),
          contactInfo: this.playdateContactInfo().trim(),
        };
      case BABYSITTER: {
        // canSave() guarantees a choice from the current list. The server ignores the name and
        // sends back the one it resolves.
        const [guardianId = '', babysitterId = ''] = this.babysitterChoice().split('|');
        return {
          kind: BABYSITTER,
          guardianId,
          babysitterId,
          name: this.chosenBabysitter()?.name ?? '',
        };
      }
    }
  }

  protected clearAssignment(): void {
    this.editing.set(false);
    this.clear.emit();
  }
}
