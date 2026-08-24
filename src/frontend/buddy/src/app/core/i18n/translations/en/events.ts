export const events = {
  list: {
    title: 'Recent events',
    liveBadge: 'Live',
    loading: 'Loading events…',
    loadError: 'Unable to load recent events.',
    empty: 'No events yet.',
    previous: 'Previous',
    next: 'Next'
  },
  types: {
    userCreated: {
      title: 'Account created',
      description: '{name} ({email}) joined via Keycloak.'
    },
    userDeleted: {
      title: 'Account deleted',
      description: 'The account was deleted.'
    },
    nameUpdated: {
      title: 'Name updated',
      description: 'Name changed from {before} to {after}.'
    },
    emailUpdated: {
      title: 'Email updated',
      description: 'Email changed from {before} to {after}.'
    },
    emailVerificationRequested: {
      title: 'Email verification requested',
      description: 'A verification link was sent, expiring {expiresAt}.'
    },
    emailVerified: {
      title: 'Email verified',
      description: 'The email address was verified.'
    }
  }
};
