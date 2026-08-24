export const admin = {
  eyebrow: 'Indstillinger',
  title: 'Administrer din husstand.',
  backToDashboard: 'Tilbage til oversigt',
  manageGroups: {
    title: 'Grupper',
    loading: 'Indlæser grupper…',
    loadError: 'Kunne ikke indlæse grupper.',
    empty: 'Ingen grupper endnu. Opret en nedenfor.',
    namePlaceholder: 'Gruppenavn',
    addButton: 'Tilføj gruppe',
    createError: 'Kunne ikke oprette gruppen.',
    roles: {
      owner: 'Ejer',
      admin: 'Administrator',
      member: 'Medlem'
    },
    invite: {
      showButton: 'Inviter',
      hideButton: 'Luk',
      pendingTitle: 'Afventende invitationer',
      pendingEmpty: 'Ingen afventende invitationer.',
      loading: 'Indlæser invitationer…',
      loadError: 'Kunne ikke indlæse invitationer.',
      emailPlaceholder: 'E-mailadresse',
      sendButton: 'Send invitation',
      sendError: 'Kunne ikke sende invitationen. E-mailen er muligvis allerede medlem, eller en invitation blev allerede sendt for nylig.',
      cancelButton: 'Annuller',
      cancelError: 'Kunne ikke annullere invitationen.'
    },
    children: {
      showButton: 'Tilføj et barn',
      hideButton: 'Luk',
      title: 'Tilføj et barn',
      loading: 'Indlæser medlemmer…',
      loadError: 'Kunne ikke indlæse gruppens medlemmer.',
      empty: 'Alle dine børn er allerede i denne gruppe.',
      selectPlaceholder: 'Vælg et barn',
      addButton: 'Tilføj til gruppe',
      addError: 'Kunne ikke tilføje barnet til gruppen.'
    },
    policy: {
      title: 'Kalendertilladelser',
      showButton: 'Kalendertilladelser',
      hideButton: 'Luk',
      loading: 'Indlæser tilladelser…',
      loadError: 'Kunne ikke indlæse kalendertilladelser.',
      saveButton: 'Gem tilladelser',
      saveError: 'Kunne ikke gemme kalendertilladelser.'
    },
    mealplanPolicy: {
      title: 'Måltidsplan-tilladelser',
      showButton: 'Måltidsplan-tilladelser',
      hideButton: 'Luk',
      loading: 'Indlæser tilladelser…',
      loadError: 'Kunne ikke indlæse måltidsplan-tilladelser.',
      saveButton: 'Gem tilladelser',
      saveError: 'Kunne ikke gemme måltidsplan-tilladelser.',
      tiers: {
        none: 'Ingen adgang',
        view: 'Kun læsning',
        manage: 'Fuld adgang'
      }
    }
  },
  manageChildren: {
    title: 'Børn',
    loading: 'Indlæser børn…',
    loadError: 'Kunne ikke indlæse børn.',
    empty: 'Ingen børn tilknyttet endnu. Tilføj et nedenfor.',
    removeConfirmPrompt: 'Fjern dette barn?',
    confirm: 'Bekræft',
    cancel: 'Annuller',
    linkedBadge: 'Tilknyttet',
    remove: 'Fjern',
    revokeError: 'Kunne ikke fjerne dette barn.',
    givenNamePlaceholder: 'Fornavn',
    familyNamePlaceholder: 'Efternavn',
    usernamePlaceholder: 'Login-brugernavn',
    addButton: 'Tilføj barn',
    usernameTakenError: 'Det brugernavn er allerede i brug. Vælg et andet.',
    addError: 'Kunne ikke oprette barnets konto.',
    createdMessage: '{name} blev oprettet.',
    temporaryPasswordLabel: 'Midlertidig adgangskode (vises kun én gang):',
    copy: 'Kopiér',
    copied: 'Kopieret!',
    invite: {
      showButton: 'Inviter en medforælder',
      hideButton: 'Luk',
      pendingTitle: 'Afventende invitationer',
      pendingEmpty: 'Ingen afventende invitationer.',
      loading: 'Indlæser invitationer…',
      loadError: 'Kunne ikke indlæse invitationer.',
      emailPlaceholder: 'E-mailadresse',
      sendButton: 'Send invitation',
      sendError: 'Kunne ikke sende invitationen. Der er for nylig sendt en invitation til denne adresse.',
      cancelButton: 'Annuller',
      cancelError: 'Kunne ikke annullere invitationen.',
      kinds: {
        parent: 'Forælder',
        guardian: 'Værge'
      }
    }
  },
  manageCalendars: {
    title: 'Kalendere',
    loading: 'Indlæser kalendere…',
    loadError: 'Kunne ikke indlæse kalendere.',
    empty: 'Ingen kalendere endnu. Opret en nedenfor.',
    namePlaceholder: 'Kalendernavn',
    addButton: 'Tilføj kalender',
    createError: 'Kunne ikke oprette kalenderen.',
    roles: {
      owner: 'Ejer',
      contributor: 'Bidragyder',
      viewer: 'Læser'
    },
    needsGroupHint: 'Du skal have en gruppe, før du kan tilføje en kalender. Opret en under Grupper først.',
    move: {
      showButton: 'Flyt til gruppe',
      hideButton: 'Luk',
      selectPlaceholder: 'Vælg en gruppe',
      confirmButton: 'Flyt',
      noGroups: 'Du skal have en anden gruppe, du administrerer, før du kan flytte denne kalender.',
      error: 'Kunne ikke flytte denne kalender. Du administrerer muligvis ikke modtagergruppen.'
    }
  },
  deleteAccount: {
    title: 'Faresone',
    description: 'Sletning af din konto fjerner din adgang permanent. Dette kan ikke fortrydes.',
    deleteButton: 'Slet min konto',
    confirmTitle: 'Slet din konto?',
    confirmDescription: 'Dette sletter din konto permanent og kan ikke fortrydes. Du bliver logget ud med det samme.',
    cancel: 'Annuller',
    confirmButton: 'Ja, slet min konto',
    deletingButton: 'Sletter…',
    error: 'Kunne ikke slette din konto.'
  }
};
