import { ChildBabysitter } from '../../../core/babysitters.service';
import { GuardianSummary, childFacingName } from '../../../core/guardians.service';
import { NIGHT_RESPONSIBLE, NightEntry, NightResponsible } from '../../../core/night-duty.service';

// Who can be on duty for the selected child: its guardians and their babysitters.
export interface NightPeople {
  guardians: GuardianSummary[];
  babysitters: ChildBabysitter[];
}

export const NO_PEOPLE: NightPeople = { guardians: [], babysitters: [] };

// A responsible person travels through the <select>s as one string value ("choice"):
// 'g:<guardianId>', 'b:<guardianId>:<babysitterId>', 's' (a sleepover; its host is typed
// separately) or '' (nobody named, a note on its own).

export const SLEEPOVER_CHOICE = 's';
export const NOBODY_CHOICE = '';

export function choiceOf(responsible: NightResponsible): string {
  switch (responsible.kind) {
    case NIGHT_RESPONSIBLE.guardian:
      return `g:${responsible.guardianId}`;
    case NIGHT_RESPONSIBLE.babysitter:
      return `b:${responsible.guardianId}:${responsible.babysitterId}`;
    case NIGHT_RESPONSIBLE.sleepover:
      return SLEEPOVER_CHOICE;
    default:
      return NOBODY_CHOICE;
  }
}

// location and contactInfo are carried through unchanged even where the UI doesn't show them, so
// a save never wipes what was stored.
export interface SleepoverDraft {
  hostName: string;
  location: string;
  contactInfo: string;
}

export function sleepoverOf(responsible: NightResponsible | null): SleepoverDraft {
  return responsible?.kind === NIGHT_RESPONSIBLE.sleepover
    ? {
        hostName: responsible.hostName,
        location: responsible.location ?? '',
        contactInfo: responsible.contactInfo ?? '',
      }
    : { hostName: '', location: '', contactInfo: '' };
}

export function responsibleFromChoice(choice: string, sleepover: SleepoverDraft): NightResponsible {
  const [kind, first = '', second = ''] = choice.split(':');

  if (kind === 'g') {
    return { kind: NIGHT_RESPONSIBLE.guardian, guardianId: first };
  }
  if (kind === 'b') {
    return { kind: NIGHT_RESPONSIBLE.babysitter, guardianId: first, babysitterId: second };
  }
  if (choice === SLEEPOVER_CHOICE) {
    return {
      kind: NIGHT_RESPONSIBLE.sleepover,
      hostName: sleepover.hostName.trim(),
      location: sleepover.location.trim(),
      contactInfo: sleepover.contactInfo.trim(),
    };
  }
  return { kind: NIGHT_RESPONSIBLE.unassigned };
}

// Whether a draft is something the API accepts as an entry: a person, a note, or both, and a
// sleepover always with a host.
export function isCompleteEntry(entry: NightEntry): boolean {
  const note = (entry.note ?? '').trim();
  const responsible = entry.responsible;

  if (responsible.kind === NIGHT_RESPONSIBLE.sleepover) {
    return responsible.hostName.trim().length > 0;
  }
  return responsible.kind !== NIGHT_RESPONSIBLE.unassigned || note.length > 0;
}

export function babysitterChoice(babysitter: ChildBabysitter): string {
  return `b:${babysitter.guardianId}:${babysitter.id}`;
}

// Translated words used when a name can't be resolved or for a sleepover.
export interface ResponsibleLabels {
  guardian: string;
  babysitter: string;
  sleepover: (host: string) => string;
}

export interface ResponsibleText {
  icon: string;
  text: string;
}

// What a night shows for its responsible person, or null when nobody is named.
export function responsibleText(
  responsible: NightResponsible,
  people: NightPeople,
  labels: ResponsibleLabels,
): ResponsibleText | null {
  switch (responsible.kind) {
    case NIGHT_RESPONSIBLE.guardian: {
      const guardian = people.guardians.find((g) => g.id === responsible.guardianId);
      return { icon: '👤', text: guardian ? childFacingName(guardian) : labels.guardian };
    }
    case NIGHT_RESPONSIBLE.babysitter:
      return { icon: '🧑‍🍼', text: (responsible.name ?? '') || labels.babysitter };
    case NIGHT_RESPONSIBLE.sleepover:
      return { icon: '🛏️', text: labels.sleepover(responsible.hostName) };
    default:
      return null;
  }
}
