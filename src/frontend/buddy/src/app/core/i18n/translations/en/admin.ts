export const admin = {
  eyebrow: 'Settings',
  title: 'Manage your household.',
  backToDashboard: 'Back to dashboard',
  manageGroups: {
    title: 'Groups',
    loading: 'Loading groups…',
    loadError: 'Unable to load groups.',
    empty: 'No groups yet. Create one below.',
    namePlaceholder: 'Group name',
    addButton: 'Add group',
    createError: 'Unable to create the group.',
    roles: {
      owner: 'Owner',
      admin: 'Admin',
      member: 'Member'
    },
    invite: {
      showButton: 'Invite',
      hideButton: 'Close',
      pendingTitle: 'Pending invites',
      pendingEmpty: 'No pending invites.',
      loading: 'Loading invites…',
      loadError: 'Unable to load invites.',
      emailPlaceholder: 'Email address',
      sendButton: 'Send invite',
      sendError: 'Unable to send the invite. The email may already be a member, or an invite was already sent recently.',
      cancelButton: 'Cancel',
      cancelError: 'Unable to cancel the invite.'
    },
    children: {
      showButton: 'Add a child',
      hideButton: 'Close',
      title: 'Add a child',
      loading: 'Loading members…',
      loadError: 'Unable to load group members.',
      empty: 'All of your children are already in this group.',
      selectPlaceholder: 'Choose a child',
      addButton: 'Add to group',
      addError: 'Unable to add this child to the group.'
    },
    policy: {
      title: 'Calendar permissions',
      showButton: 'Calendar permissions',
      hideButton: 'Close',
      loading: 'Loading permissions…',
      loadError: 'Unable to load calendar permissions.',
      saveButton: 'Save permissions',
      saveError: 'Unable to save calendar permissions.'
    },
    mealplanPolicy: {
      title: 'Meal plan permissions',
      showButton: 'Meal plan permissions',
      hideButton: 'Close',
      loading: 'Loading permissions…',
      loadError: 'Unable to load meal plan permissions.',
      saveButton: 'Save permissions',
      saveError: 'Unable to save meal plan permissions.',
      tiers: {
        none: 'No access',
        view: 'Read only',
        manage: 'Full access'
      }
    }
  },
  manageChildren: {
    title: 'Children',
    loading: 'Loading children…',
    loadError: 'Unable to load children.',
    empty: 'No children linked yet. Add one below.',
    removeConfirmPrompt: 'Remove this child?',
    confirm: 'Confirm',
    cancel: 'Cancel',
    linkedBadge: 'Linked',
    remove: 'Remove',
    revokeError: 'Unable to remove this child.',
    language: {
      label: 'Language',
      error: 'Unable to update this child\'s language.'
    },
    givenNamePlaceholder: 'Given name',
    familyNamePlaceholder: 'Family name',
    usernamePlaceholder: 'Login username',
    addButton: 'Add child',
    usernameTakenError: 'That username is already in use. Choose another one.',
    addError: 'Unable to create the child account.',
    createdMessage: '{name} was created.',
    temporaryPasswordLabel: 'Temporary password (shown once):',
    copy: 'Copy',
    copied: 'Copied!',
    invite: {
      showButton: 'Invite a co-guardian',
      hideButton: 'Close',
      pendingTitle: 'Pending invites',
      pendingEmpty: 'No pending invites.',
      loading: 'Loading invites…',
      loadError: 'Unable to load invites.',
      emailPlaceholder: 'Email address',
      sendButton: 'Send invite',
      sendError: 'Unable to send the invite. An invite was already sent to this address recently.',
      cancelButton: 'Cancel',
      cancelError: 'Unable to cancel the invite.',
      kinds: {
        parent: 'Parent',
        guardian: 'Guardian'
      }
    }
  },
  manageCalendars: {
    title: 'Calendars',
    loading: 'Loading calendars…',
    loadError: 'Unable to load calendars.',
    empty: 'No calendars yet. Create one below.',
    namePlaceholder: 'Calendar name',
    addButton: 'Add calendar',
    createError: 'Unable to create the calendar.',
    roles: {
      owner: 'Owner',
      contributor: 'Contributor',
      viewer: 'Viewer'
    },
    needsGroupHint: 'You need a group before you can add a calendar. Create one under Groups first.',
    move: {
      showButton: 'Move to group',
      hideButton: 'Close',
      selectPlaceholder: 'Choose a group',
      confirmButton: 'Move',
      noGroups: 'You need another group you manage before you can move this calendar.',
      error: 'Unable to move this calendar. You may not manage the destination group.'
    },
    delete: {
      button: 'Delete',
      confirmPrompt: 'Delete this calendar? This cannot be undone.',
      confirmButton: 'Confirm',
      cancelButton: 'Cancel',
      error: 'Unable to delete this calendar.'
    },
    ical: {
      showButton: 'Subscribe',
      hideButton: 'Close',
      description: 'Generate a private link to subscribe to this calendar in an external calendar app (e.g. Google Calendar, Apple Calendar, Outlook).',
      loading: 'Loading links…',
      loadError: 'Unable to load subscription links.',
      empty: 'No subscription links yet.',
      issuedOn: 'Created',
      createButton: 'Generate new link',
      creating: 'Generating…',
      createError: 'Unable to generate a subscription link.',
      newUrlHint: 'Copy this link now -- it will not be shown again.',
      copyButton: 'Copy',
      copiedButton: 'Copied',
      revokeButton: 'Revoke',
      revokeError: 'Unable to revoke this link.'
    }
  },
  deleteAccount: {
    title: 'Danger zone',
    description: 'Deleting your account removes your access permanently. This cannot be undone.',
    deleteButton: 'Delete my account',
    confirmTitle: 'Delete your account?',
    confirmDescription: 'This permanently deletes your account and cannot be undone. You’ll be signed out immediately.',
    cancel: 'Cancel',
    confirmButton: 'Yes, delete my account',
    deletingButton: 'Deleting…',
    error: 'Unable to delete your account.'
  }
};
