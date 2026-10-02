import { UserEventItem } from '../../../../core/user-events.service';

export interface EmailSummary {
  value: string;
  isVerified: boolean;
}

export interface NameSummary {
  givenName: string;
  familyName: string;
}

export interface UserCreatedData {
  userId: string;
  keycloakSubject: string;
  email: EmailSummary;
  userName: string | null;
  name: NameSummary;
  occurredAt: string;
}

export interface UserDeletedData {
  userId: string;
  occurredAt: string;
}

export interface NameUpdatedData {
  userId: string;
  before: NameSummary;
  after: NameSummary;
  occurredAt: string;
}

export interface EmailUpdatedData {
  userId: string;
  before: EmailSummary;
  after: EmailSummary;
  occurredAt: string;
}

export interface EmailVerificationRequestedData {
  userId: string;
  expiresAt: string;
  occurredAt: string;
}

export interface EmailVerifiedData {
  userId: string;
  occurredAt: string;
}

export interface TimeZoneUpdatedData {
  userId: string;
  before: string;
  after: string;
  occurredAt: string;
}

export interface LanguageUpdatedData {
  userId: string;
  before: string;
  after: string;
  occurredAt: string;
}

// Known event payloads, keyed by the backend's event type name. The events list switches on
// `type` and each case's `data` arrives already narrowed for its component.
type KnownUserEvent =
  | { type: 'UserCreated'; data: UserCreatedData }
  | { type: 'UserDeleted'; data: UserDeletedData }
  | { type: 'NameUpdated'; data: NameUpdatedData }
  | { type: 'EmailUpdated'; data: EmailUpdatedData }
  | { type: 'EmailVerificationRequested'; data: EmailVerificationRequestedData }
  | { type: 'EmailVerified'; data: EmailVerifiedData }
  | { type: 'TimeZoneUpdated'; data: TimeZoneUpdatedData }
  | { type: 'LanguageUpdated'; data: LanguageUpdatedData };

export type TypedUserEvent =
  KnownUserEvent | { type: 'Unknown'; rawType: string; data: Record<string, unknown> };

// `satisfies` keeps this in step with KnownUserEvent: a new union member without a key here fails
// to compile.
const KNOWN_USER_EVENT_TYPES = {
  UserCreated: true,
  UserDeleted: true,
  NameUpdated: true,
  EmailUpdated: true,
  EmailVerificationRequested: true,
  EmailVerified: true,
  TimeZoneUpdated: true,
  LanguageUpdated: true,
} satisfies Record<KnownUserEvent['type'], true>;

// The API sends each payload as untyped JSON next to its event type name. This is the one place
// that trusts the name to describe the payload's shape.
export function toTypedUserEvent(item: UserEventItem): TypedUserEvent {
  return Object.hasOwn(KNOWN_USER_EVENT_TYPES, item.type)
    ? (item as unknown as KnownUserEvent)
    : { type: 'Unknown', rawType: item.type, data: item.data };
}
