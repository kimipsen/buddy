export const dashboard = {
  eyebrow: 'I dag',
  greeting: 'Godt at se dig.',
  children: {
    title: 'Børn',
    loading: 'Indlæser børn…',
    loadError: 'Kunne ikke indlæse børn.',
    empty: 'Ingen børn tilknyttet endnu. Tilføj et under Indstillinger.',
    linkedBadge: 'Tilknyttet'
  },
  tasks: {
    title: 'Dagens opgaver',
    loading: 'Indlæser opgaver…',
    loadError: 'Kunne ikke indlæse dagens opgaver.',
    taskUpdateError: 'Kunne ikke opdatere denne opgave.',
    empty: 'Ingen opgaver forfalder i dag.',
    overdue: 'Forsinket',
    dueToday: 'Forfalder i dag'
  },
  events: {
    title: 'Dagens begivenheder',
    loading: 'Indlæser begivenheder…',
    loadError: 'Kunne ikke indlæse dagens begivenheder.',
    empty: 'Intet andet i kalenderen i dag.'
  },
  mealplan: {
    title: 'Dagens madplan',
    planLink: 'Planlæg måltider →',
    loading: 'Indlæser madplan…',
    loadError: 'Kunne ikke indlæse dagens madplan.',
    noChildren: 'Tilknyt et barn under Indstillinger for at se deres madplan.',
    notPlanned: 'Ikke planlagt',
    slots: {
      breakfast: 'Morgenmad',
      lunch: 'Frokost',
      dinner: 'Aftensmad',
      snack: 'Mellemmåltid'
    }
  },
  doses: {
    title: 'Dagens medicin',
    manageLink: 'Administrer medicin →',
    loading: 'Indlæser dagens doser…',
    loadError: 'Kunne ikke indlæse dagens medicindoser.',
    updateError: 'Kunne ikke opdatere denne dosis.',
    noChildren: 'Tilknyt et barn under Indstillinger for at følge deres medicin.',
    empty: 'Ingen medicin planlagt til i dag.',
    markTaken: 'Marker som taget',
    skip: 'Spring over',
    taken: 'Taget',
    skipped: 'Sprunget over',
    undo: 'Fortryd'
  },
  pickup: {
    title: 'Dagens afhentning & aflevering',
    manageLink: 'Planlæg afhentning →',
    loading: 'Indlæser dagens plan…',
    loadError: 'Kunne ikke indlæse dagens afhentningsplan.',
    noChildren: 'Tilknyt et barn under Indstillinger for at planlægge deres afhentning.',
    empty: 'Intet planlagt for i dag.',
    slots: {
      dropOff: 'Aflevering',
      pickUp: 'Afhentning'
    },
    kind: {
      guardian: 'En voksen',
      selfEscort: 'Går selv',
      sibling: 'En søskende'
    }
  }
};
