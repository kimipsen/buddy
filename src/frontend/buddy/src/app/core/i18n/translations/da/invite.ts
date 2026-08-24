export const invite = {
  preview: {
    loading: 'Indlæser invitation…',
    notFoundError: 'Dette invitationslink er ugyldigt eller udløbet.',
    title: 'Du er blevet inviteret til at deltage i {groupName}.',
    logInPrompt: 'Log ind med den konto, invitationen blev sendt til, for at acceptere den.',
    logInButton: 'Log ind for at acceptere'
  },
  accept: {
    acceptButton: 'Acceptér invitation',
    error: 'Kunne ikke acceptere denne invitation. Den er muligvis udløbet eller allerede brugt.',
    wrongAccountError: 'Denne invitation blev sendt til en anden konto, end den du er logget ind med.',
    successTitle: 'Du er nu medlem af {groupName}.',
    goToGroupsButton: 'Gå til mine grupper'
  },
  guardianPreview: {
    loading: 'Indlæser invitation…',
    notFoundError: 'Dette invitationslink er ugyldigt eller udløbet.',
    title: 'Du er blevet inviteret til at hjælpe med at administrere {childGivenName}s konto, som {kind}.',
    logInPrompt: 'Log ind med den konto, invitationen blev sendt til, for at acceptere den.',
    logInButton: 'Log ind for at acceptere',
    kinds: {
      parent: 'forælder',
      guardian: 'værge'
    }
  },
  guardianAccept: {
    acceptButton: 'Acceptér invitation',
    error: 'Kunne ikke acceptere denne invitation. Den er muligvis udløbet eller allerede brugt.',
    wrongAccountError: 'Denne invitation blev sendt til en anden konto, end den du er logget ind med.',
    successTitle: 'Du er nu værge for {childGivenName}.',
    goToChildrenButton: 'Gå til mine børn'
  }
};
