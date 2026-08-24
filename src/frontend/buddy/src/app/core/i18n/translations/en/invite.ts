export const invite = {
  preview: {
    loading: 'Loading invite…',
    notFoundError: 'This invite link is invalid or has expired.',
    title: "You've been invited to join {groupName}.",
    logInPrompt: 'Log in with the account this invite was sent to, to accept it.',
    logInButton: 'Log in to accept'
  },
  accept: {
    acceptButton: 'Accept invite',
    error: 'Unable to accept this invite. It may have expired or already been used.',
    wrongAccountError: "This invite was sent to a different account than the one you're logged in with.",
    successTitle: "You've joined {groupName}.",
    goToGroupsButton: 'Go to my groups'
  },
  guardianPreview: {
    loading: 'Loading invite…',
    notFoundError: 'This invite link is invalid or has expired.',
    title: "You've been invited to help manage {childGivenName}'s account, as a {kind}.",
    logInPrompt: 'Log in with the account this invite was sent to, to accept it.',
    logInButton: 'Log in to accept',
    kinds: {
      parent: 'parent',
      guardian: 'guardian'
    }
  },
  guardianAccept: {
    acceptButton: 'Accept invite',
    error: 'Unable to accept this invite. It may have expired or already been used.',
    wrongAccountError: "This invite was sent to a different account than the one you're logged in with.",
    successTitle: "You're now a guardian for {childGivenName}.",
    goToChildrenButton: 'Go to my children'
  }
};
