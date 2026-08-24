export const events = {
  list: {
    title: 'Seneste begivenheder',
    liveBadge: 'Live',
    loading: 'Indlæser begivenheder…',
    loadError: 'Kunne ikke indlæse seneste begivenheder.',
    empty: 'Ingen begivenheder endnu.',
    previous: 'Forrige',
    next: 'Næste'
  },
  types: {
    userCreated: {
      title: 'Konto oprettet',
      description: '{name} ({email}) tilmeldte sig via Keycloak.'
    },
    userDeleted: {
      title: 'Konto slettet',
      description: 'Kontoen blev slettet.'
    },
    nameUpdated: {
      title: 'Navn opdateret',
      description: 'Navn ændret fra {before} til {after}.'
    },
    emailUpdated: {
      title: 'E-mail opdateret',
      description: 'E-mail ændret fra {before} til {after}.'
    },
    emailVerificationRequested: {
      title: 'E-mailbekræftelse anmodet',
      description: 'Der blev sendt et bekræftelseslink, som udløber {expiresAt}.'
    },
    emailVerified: {
      title: 'E-mail bekræftet',
      description: 'E-mailadressen blev bekræftet.'
    }
  }
};
