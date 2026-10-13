import type { WarningKind } from '../../../core/transition-warnings';
import type {
  ChildTransitionWarnings,
  ConfigureTransitionWarningsRequest,
} from '../../../core/transition-warnings.service';

// The lead times a guardian picks from (Question 3 of the design: one to three of these).
export const LEAD_CHOICES: readonly number[] = [30, 20, 15, 10, 5, 2, 1];
export const MAX_LEAD_TIMES = 3;

export const ALL_KINDS: readonly WarningKind[] = ['Event', 'Task', 'DropOff', 'PickUp'];

// What a fresh quiet-hours window starts at when a guardian turns it on.
const DEFAULT_QUIET_FROM = '21:00';
const DEFAULT_QUIET_TO = '07:00';

// The settings form: the server's settings, plus quiet hours as an on/off switch with two times,
// so turning quiet hours off and on again keeps the times typed.
export interface WarningDraft {
  enabled: boolean;
  leadMinutes: number[];
  kinds: WarningKind[];
  quietOn: boolean;
  quietFrom: string;
  quietTo: string;
}

// "HH:mm:ss" from the API to the "HH:mm" app-time-select uses.
function hhmm(time: string): string {
  return time.slice(0, 5);
}

export function draftFrom(settings: ChildTransitionWarnings): WarningDraft {
  return {
    enabled: settings.enabled,
    leadMinutes: [...settings.leadMinutes],
    kinds: [...settings.kinds],
    quietOn: settings.quietHours !== null,
    quietFrom: settings.quietHours ? hhmm(settings.quietHours.from) : DEFAULT_QUIET_FROM,
    quietTo: settings.quietHours ? hhmm(settings.quietHours.to) : DEFAULT_QUIET_TO,
  };
}

export function requestFrom(draft: WarningDraft): ConfigureTransitionWarningsRequest {
  return {
    enabled: draft.enabled,
    leadMinutes: [...draft.leadMinutes].sort((a, b) => b - a),
    kinds: ALL_KINDS.filter((kind) => draft.kinds.includes(kind)),
    quietHours: draft.quietOn ? { from: draft.quietFrom, to: draft.quietTo } : null,
  };
}

// Whether saving the draft would change what the server has.
export function isDirty(draft: WarningDraft, settings: ChildTransitionWarnings): boolean {
  return JSON.stringify(requestFrom(draft)) !== JSON.stringify(requestFrom(draftFrom(settings)));
}

// A lead-time chip list with `minutes` turned on or off, never more than MAX_LEAD_TIMES.
export function toggleLead(leadMinutes: readonly number[], minutes: number): number[] {
  if (leadMinutes.includes(minutes)) {
    return leadMinutes.filter((lead) => lead !== minutes);
  }
  return leadMinutes.length >= MAX_LEAD_TIMES
    ? [...leadMinutes]
    : [...leadMinutes, minutes].sort((a, b) => b - a);
}

// The chips to show: the usual choices, plus any other value already saved (set through the API).
export function leadChoices(leadMinutes: readonly number[]): number[] {
  return [...new Set([...LEAD_CHOICES, ...leadMinutes])].sort((a, b) => b - a);
}
