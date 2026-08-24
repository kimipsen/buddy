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
