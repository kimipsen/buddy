export const help = {
  panelTitle: 'Hjælp: {topic}',
  close: 'Luk hjælp',
  related: 'Se også:',
  allTopics: 'Alle hjælpeemner',
  page: {
    backLink: 'Tilbage til oversigt',
    eyebrow: 'Hjælp',
    title: 'Sådan virker Buddy',
    intro:
      'Alle hjælpeemner samlet ét sted. Knappen "?" øverst på en side åbner hjælpen til den side.',
    contentsLabel: 'Hjælpeemner',
    contact:
      'Virker noget ikke? Spørg den, der driver Buddy for jeres familie, eller se projektet på',
    repositoryLink: 'GitHub',
  },
  topics: {
    dashboard: {
      title: 'Dashboard',
      link: 'Fortsæt den guidede opsætning, hvis du ikke er færdig med den',
      sections: {
        overview: {
          title: 'Dagen på ét blik',
          body: 'Dashboardet viser familiens dag i dag: måltider, opgaver, begivenheder, medicin, afhentning og dine børn. Hvert kort fører videre til sin egen side, når du har brug for mere. Brug "Print ugeplan" til at printe ugen. Har du sat den guidede opsætning på pause, kan du gøre den færdig med "Fortsæt opsætningen".',
        },
        tasks: {
          title: 'Sæt flueben ved dagens opgaver',
          body: '"Dagens opgaver" viser det, der forfalder i dag. Opgaver, hvis tidspunkt er passeret uden at de er udført, står under "Forsinket". Du kan markere en opgave som udført, hvis den er tildelt dig eller ingen. En rutine fra en skabelon viser, hvor mange af trinnene der er udført. "Dagens begivenheder" viser resten af dagens kalender.',
        },
        medicine: {
          title: 'Registrér medicindoser',
          body: 'Under "Dagens medicin" ser du hver dosis med tidspunkt. Tryk "Marker som taget", når barnet har fået den, eller "Spring over", hvis dosen udelades. Kom du til at trykke forkert? "Fortryd" sætter dosen tilbage. Vil du ændre selve medicinplanerne, så brug "Administrer medicin →".',
        },
        mealsAndPickups: {
          title: 'Måltider, afhentning og stjerner',
          body: '"Dagens madplan" viser morgenmad, frokost, aftensmad og mellemmåltid, og "Planlæg måltider →" åbner madplanen. "Dagens afhentning & aflevering" viser, hvem der afleverer og henter hvert barn, og du planlægger det med "Planlæg afhentning →". Kortet "Børn" viser dine børn og de stjerner, de har samlet mod deres mål.',
        },
      },
    },
    calendar: {
      title: 'Kalendere',
      sections: {
        views: {
          title: 'Dag, uge og måned',
          body: 'Skift mellem "Dag", "Arbejdsuge", "Uge" og "Måned". "Uge" viser syv dage fra den dag, du står på, og "Arbejdsuge" viser mandag til fredag. Brug "I dag" og knapperne for forrige og næste til at bladre. I "Måned" vælger du en dag for at åbne den. Har du flere kalendere, kan du skjule eller vise hver enkelt under "Kalendere".',
        },
        ownership: {
          title: 'Hvem ejer en kalender',
          body: 'Alle kalendere hører til en gruppe; der findes ingen private kalendere. Gruppens kalendertilladelser bestemmer, hvad hvert medlem kan. Som udgangspunkt kan gruppens ejer redigere og styre kalenderen, administratorer kan redigere den, og medlemmer kan se den. Et barn ser begivenhederne og kun de opgaver, der er tildelt barnet. Du opretter kalendere under Kalendere i Indstillinger.',
        },
        addItem: {
          title: 'Tilføj en begivenhed eller opgave',
          body: 'En begivenhed optager tid, fx et tandlægebesøg. En opgave er noget, der skal gøres til et bestemt tidspunkt, og den kan markeres som udført. Du kan tilføje til alle kalendere, du kan redigere.',
          steps: {
            s1: 'Under "Tilføj en begivenhed eller opgave" vælger du kalenderen og "Begivenhed" eller "Opgave".',
            s2: 'Skriv en titel, og vælg gerne et ikon og en farve.',
            s3: 'Angiv start og slut, eller forfaldsdato og -tid for en opgave, eller slå "Hele dagen" til.',
            s4: 'Brug "Gentag", hvis den kommer igen. Ved en opgave vælger du under "Tildel til", hvem den er til.',
            s5: 'Tryk "Tilføj til kalender".',
          },
        },
        changeItems: {
          title: 'Ret, slet eller markér som udført',
          body: 'Hver begivenhed og opgave har "Rediger" og "Slet". Med "Rediger" ændrer du titel, ikon, farve og tidspunkter. "Slet" beder dig bekræfte først. Brug opgavens kontakt til at markere den som udført; for en gentaget opgave gælder det kun den dag. En rutine fra en skabelon kan kun slettes, og den viser hvert trin med sin egen kontakt.',
        },
      },
    },
    taskLibrary: {
      title: 'Opgavebibliotek',
      sections: {
        templates: {
          title: 'Rutiner som skabeloner',
          body: 'En opgaveskabelon er en rutine, du kan bruge igen og igen, fx at gøre sig klar til skole, delt op i trin i en fast rækkefølge. Hvert trin tager et bestemt antal minutter, og skabelonen viser den samlede tid. Hver skabelon hører til ét barn. Har du flere børn, vælger du først barnet øverst i listen.',
        },
        buildTemplate: {
          title: 'Byg en rutine',
          body: 'Hold trinnene korte og konkrete, så de er nemme at følge ét ad gangen.',
          steps: {
            s1: 'Skriv et "Skabelonnavn", vælg gerne et ikon og en farve, og tryk "Tilføj skabelon".',
            s2: 'Tryk "Rediger deltrin" på den nye skabelon.',
            s3: 'Skriv en "Deltrinstitel" for hvert trin, angiv minutterne, og tryk "Tilføj deltrin".',
            s4: 'Brug pilene op og ned til at ændre trinnenes rækkefølge.',
          },
        },
        schedule: {
          title: 'Sæt en rutine i kalenderen',
          body: 'Du planlægger en skabelon fra kalendersiden. Formularen viser, hvor lang tid rutinen tager, og hvornår den slutter, og hvert trin kommer i kalenderen på sit eget tidspunkt.',
          steps: {
            s1: 'Under "Tilføj en begivenhed eller opgave" vælger du "Opgave" og derefter "Fra skabelon".',
            s2: 'Vælg under "Tildel til" det barn, skabelonen hører til.',
            s3: 'Vælg rutinen under "Opgaveskabelon".',
            s4: 'Angiv "Forfaldsdato" og "Forfaldstid", hvor rutinen starter, og tryk "Tilføj til kalender".',
          },
        },
        archive: {
          title: 'Omdøb eller arkivér en rutine',
          body: 'Brug "Omdøb" til at ændre en skabelons navn, ikon eller farve. Når en rutine ikke bruges mere, trykker du "Arkiver". Den bliver stående i listen som "Arkiveret", men du kan ikke længere sætte den i kalenderen.',
        },
      },
    },
    mealPlans: {
      title: 'Madplaner',
      sections: {
        planWeek: {
          title: 'Planlæg ugens måltider',
          body: 'Dine måltider ligger i ét bibliotek, som alle dine børn deler, så du kun planlægger hver dag én gang. Under "Måltider" skriver du et navn og trykker "Tilføj måltid". Buddy siger til, hvis der allerede findes et lignende måltid. Med "Arkiver" fjerner du et måltid, du ikke laver mere. Børnene kan give måltider stjerner, og deres bedømmelser vises under hvert planlagt måltid.',
          steps: {
            s1: 'Under "Ugens plan" vælger du et måltid til morgenmad, frokost, aftensmad eller mellemmåltid på en dag.',
            s2: 'Træk et planlagt måltid for at flytte det eller bytte det med en anden dag.',
            s3: 'Brug "Næste uge →" og "← Forrige uge" til at planlægge længere frem.',
          },
        },
        aiAssistant: {
          title: 'Få AI-assistenten til at lave et udkast',
          body: 'Assistenten foreslår måltider fra jeres eget bibliotek. Den skal bruge en API-nøgle fra en AI-udbyder, som du tilføjer under Indstillinger i "AI-madplansassistent". Første gang læser du, hvad den deler, og trykker "Jeg forstår, fortsæt". Du kan begrænse den til måltider, børnene har bedømt, eller måltider serveret inden for de sidste 30, 60 eller 90 dage.',
          steps: {
            s1: 'Tryk "Planlæg med AI" på madplanssiden.',
            s2: 'Vælg datoer og måltider, og tryk "Start planlægning".',
            s3: 'Skriv med assistenten, til udkastet til planen ser rigtigt ud.',
            s4: 'Tryk "Tilføj til min madplan", eller "Kassér" for at droppe udkastet.',
          },
        },
        importPlans: {
          title: 'Importér ældre madplaner',
          body: 'Har du skrevet madplaner i en note eller et regneark, kan du hente dem ind. Indsæt teksten, eller upload en tekst- eller CSV-fil. Intet bliver gemt, før du har tjekket resultatet, og dage, du allerede har planlagt, beholdes. Under "Tidligere importer" fjerner "Fortryd" de importerede dage, som ingen har ændret siden.',
          steps: {
            s1: 'Tryk "Importér ældre madplaner" på madplanssiden.',
            s2: 'Indsæt din tekst eller upload en fil, og tryk "Vis import".',
            s3: 'Vælg for hver ret, om du vil bruge en eksisterende, oprette en ny eller springe den over.',
            s4: 'Tryk på importknappen, som viser, hvor mange dage der kommer med.',
          },
        },
        calendarLink: {
          title: 'Se måltiderne i din kalenderapp',
          body: 'Under "Kalenderabonnement" trykker du "Opret abonnementslink" for at få et privat link til madplanen. Kopiér det til din kalenderapp med det samme, for Buddy viser det kun én gang. Alle med linket kan se madplanen. Tryk "Tilbagekald" ud for et link, når det ikke længere skal virke.',
        },
      },
    },
    medicine: {
      title: 'Medicin',
      sections: {
        addSchedule: {
          title: 'Tilføj en medicinplan',
          body: 'En medicinplan siger, hvilken medicin dit barn tager, hvor meget, og på hvilke tidspunkter hver dag. Har du flere børn, vælger du først barnet. Lad slutdatoen stå tom, hvis medicinen er løbende.',
          steps: {
            s1: 'Skriv "Medicinnavn" og dosis, fx 5 ml.',
            s2: 'Vælg det første tidspunkt under "Doseringstidspunkter".',
            s3: 'Vælg en "Startdato" og eventuelt en "Slutdato (valgfri)".',
            s4: 'Tryk "Tilføj plan".',
          },
        },
        doseTimes: {
          title: 'Giv medicin flere gange om dagen',
          body: 'Tryk "+ Tilføj endnu et tidspunkt" for hvert ekstra tidspunkt på dagen, og "Fjern" for at slette et. Tidspunkterne gentages hver dag fra startdatoen til slutdatoen. Når medicinen ikke skal gives mere, trykker du "Stop" ud for planen og derefter "Bekræft".',
        },
        doseStatus: {
          title: 'Marker en dosis som taget eller sprunget over',
          body: 'Dagens doser står på oversigten under "Dagens medicin". Tryk "Marker som taget", når dit barn har fået dosen, eller "Spring over", hvis den blev droppet. Trykkede du forkert? "Fortryd" sætter dosen tilbage. Dit barn kan også selv markere sine doser på sin startside.',
        },
      },
    },
    sleepDiary: {
      title: 'Søvndagbog',
      sections: {
        logNight: {
          title: 'Registrér en nat',
          body: 'Dagbogen har én række pr. nat med de samme felter som søvnklinikkens skema. Alle felter er valgfrie. Buddy foreslår, hvor længe dit barn har sovet i alt ud fra tiderne, og du kan rette det. Vil du ændre en nat, trykker du "Redigér" i listen over nætter, eller "Ryd denne nat" for at fjerne den.',
          steps: {
            s1: 'Vælg datoen under "Natten til".',
            s2: 'Udfyld sengetid, hvornår dit barn faldt i søvn, opvågninger og lure.',
            s3: 'Tryk "Gem nat".',
          },
        },
        hygieneNotes: {
          title: 'Skriv jeres søvnrutiner ned',
          body: 'Under "Søvnhygiejniske tiltag" noterer du de rutiner og ritualer, der gælder for hele dagbogen og ikke én nat, fx "ingen skærme efter kl. 19". Tryk "Gem noter". Lægen ser noterne sammen med nætterne.',
        },
        shareWithDoctor: {
          title: 'Del dagbogen med en læge',
          body: 'Med et delingslink kan en læge læse dagbogen uden at have en konto i Buddy. Lægen ser 14 dage ad gangen, altid med dine nyeste registreringer, og kan udskrive siden. Alle med linket kan læse den, så tryk "Tilbagekald" under "Aktive links", når I ikke har brug for den længere.',
          steps: {
            s1: 'Vælg under "Del med en læge", hvornår linket udløber, eller "Aldrig".',
            s2: 'Tryk "Opret link".',
            s3: 'Kopiér linket med det samme, for det vises kun én gang, og send det til lægen.',
          },
        },
      },
    },
    progress: {
      title: 'Point',
      sections: {
        earnStars: {
          title: 'Sådan tjener dit barn stjerner',
          body: 'Dit barn får en stjerne, hver gang en opgave, der er tildelt barnet, bliver markeret som klar. Har opgaven deltrin, giver hvert deltrin, barnet krydser af, en stjerne. Markeres opgaven som ikke klar igen, forsvinder stjernen. Dit barn kan se sine stjerner og sit næste mål på sin startside.',
        },
        goalPosts: {
          title: 'Sæt dit barns mål',
          body: 'Mål er de antal stjerner, dit barn arbejder hen imod, hver med et ikon og en valgfri etiket. Indtil du ændrer dem, bruger Buddy 5, 10, 25, 50 og 100 stjerner. Hvert mål skal kræve flere stjerner end det forrige.',
          steps: {
            s1: 'Vælg barnet, hvis du har flere.',
            s2: 'Skriv "Stjerner nødvendige" og et ikon for hvert mål.',
            s3: 'Tryk "+ Tilføj endnu et mål" for flere, eller "Fjern" for at slette et.',
            s4: 'Tryk "Gem mål".',
          },
        },
        milestones: {
          title: 'Når et mål er nået',
          body: 'Når dit barns stjerner når et mål, låser barnet det op, og målets ikon vises på barnets startside. Målene slipper aldrig op: Når dit barn er forbi det sidste, dukker der nye mål op med flere stjerner, som genbruger dine ikoner.',
        },
      },
    },
    pickup: {
      title: 'Afhentning og aflevering',
      sections: {
        planSlot: {
          title: 'Planlæg en afhentning eller aflevering',
          body: 'Planen viser de næste syv dage fra i dag, med en "Aflevering" og en "Afhentning" for hver dag. Har du flere børn, vælger du først barnet øverst. Alle barnets voksne kan ændre planen.',
          steps: {
            s1: 'Klik på "Ikke planlagt" ud for den dag og tid, du vil planlægge.',
            s2: 'Vælg, hvem der tager sig af det.',
            s3: 'Tilføj et tidspunkt og noter, hvis du vil.',
            s4: 'Klik på "Gem".',
          },
        },
        whoTakesCare: {
          title: 'Vælg, hvem der tager sig af det',
          body: '"En voksen" er en af barnets voksne. "En søskende" er et af dine andre børn. "Går selv" betyder, at barnet klarer turen uden en voksen. Til "Legeaftale" skal du skrive, hvem der er vært, mens sted og kontaktoplysninger er valgfrie. Med "Babysitter" vælger du en, som du eller barnets andre voksne har gemt.',
        },
        changeOrClear: {
          title: 'Ret eller ryd en plan',
          body: 'Klik på en planlagt tid for at ændre den, eller klik på "Ryd" under den for at sætte den tilbage til "Ikke planlagt". "Ikke planlagt" betyder, at ingen har besluttet noget endnu, mens "Går selv" er et bevidst valg om, at barnet ikke skal følges den dag.',
        },
        whereItShows: {
          title: 'Hvor planen bliver vist',
          body: 'Dagens plan står på din oversigt under "Dagens afhentning & aflevering". Dit barn ser dagens afhentning og aflevering på sin egen forside, også en babysitters navn, men aldrig kontaktoplysningerne. En printskabelon kan også tage planen med på den printede ugeplan.',
        },
      },
    },
    babysitters: {
      title: 'Babysittere',
      sections: {
        addBabysitter: {
          title: 'Gem en babysitter',
          body: 'Gem de babysittere og barnepiger, der henter eller afleverer dine børn, én gang, så kan du vælge dem, hver gang du planlægger en afhentning. Kontaktoplysninger som telefon eller e-mail er valgfrie.',
          steps: {
            s1: 'Skriv babysitterens "Navn".',
            s2: 'Tilføj "Kontaktoplysninger (valgfrit)", hvis du vil.',
            s3: 'Klik på "Tilføj babysitter".',
          },
        },
        useInPickups: {
          title: 'Brug en babysitter i afhentningsplanen',
          body: 'Vælg "Babysitter" i afhentningsplanen, og vælg et navn. Listen rummer de babysittere, som alle barnets voksne har gemt, så du kan bruge din medforælders babysittere, og de kan bruge dine. Dit barn ser babysitterens navn, men aldrig kontaktoplysningerne. I en printskabelon kan du give hver babysitter en navnefarve.',
        },
        editOrRemove: {
          title: 'Ret eller fjern en babysitter',
          body: 'Klik på "Rediger" for at ændre navn eller kontaktoplysninger, eller på "Fjern" for at tage en person af listen. Du kan kun ændre din egen liste. En fjernet babysitter kan ikke vælges til nye afhentninger, men allerede planlagte beholder navnet. Vil du ændre sådan en afhentning, skal du vælge en anden.',
        },
      },
    },
    workLocations: {
      title: 'Arbejdssteder',
      sections: {
        addLocations: {
          title: 'Tilføj de steder, du arbejder',
          body: 'Tilføj hvert sted, du arbejder, fx et kontor eller en anden by, med et ikon, et navn og en farve. Navnene skal være forskellige. Kun du og dine børns andre forældre kan se dine arbejdssteder. Børnene ser dem aldrig.',
          steps: {
            s1: 'Vælg et "Ikon".',
            s2: 'Skriv et "Navn", fx Kontoret.',
            s3: 'Vælg en farve.',
            s4: 'Klik på "Tilføj sted".',
          },
        },
        weeklyPattern: {
          title: 'Lav din faste uge',
          body: 'Ugemønsteret viser, hvor du plejer at være på hver ugedag. Skifter dine uger, fx kontoret den ene uge og hjemme den næste, kan mønsteret gentages hver 2. til 4. uge. Ugerne hedder A, B og så videre og gentages i rækkefølge.',
          steps: {
            s1: 'Vælg under "Gentages hver", hvor mange uger mønsteret varer.',
            s2: 'Varer det mere end én uge, så vælg under "Denne uge er", hvilken uge vi er i nu.',
            s3: 'Vælg et sted for hver dag, du arbejder.',
            s4: 'Klik på "Gem mønster".',
          },
        },
        exceptions: {
          title: 'Ændr en enkelt dag eller en ferie',
          body: 'Under "Undtagelser" vælger du et sted eller "Fri" for en dag. Ændringen gemmes med det samme og går forud for mønsteret. Vælg "Følg mønster" for at fortryde den. Brug "Tidligere" og "Senere" til at skifte uge. Til en ferie bruger du "Vælg en periode" med "Fra" og "Til" og klikker på "Anvend". En periode kan højst være en måned.',
        },
        whyItMatters: {
          title: 'Hvad det bruges til',
          body: 'Dine arbejdssteder udfylder den ugeplan, du printer. En "Arbejdssted"-række i en printskabelon viser, hvor en forælder er hver dag, eller markerer de dage, hvor forælderen er på et bestemt sted. De vises ikke i kalenderen, i afhentningsplanen eller i dit barns app.',
        },
      },
    },
    print: {
      title: 'Print ugeplan',
      sections: {
        printWeekPlan: {
          title: 'Print en ugeplan',
          body: 'En ugeplan dækker syv dage fra den første dag, du vælger, på A4 eller A3 i liggende format, som skabelonen angiver. På forhåndsvisningen kan du stadig ændre "Første dag" og slå "Medtag deltrin" til. Appen husker den skabelon, du sidst brugte på denne enhed.',
          steps: {
            s1: 'Vælg en "Skabelon" under "Hurtig print".',
            s2: 'Vælg "Første dag".',
            s3: 'Klik på "Vis".',
            s4: 'Klik på "Print".',
          },
        },
        createTemplate: {
          title: 'Opret og del en skabelon',
          body: 'En skabelon er et gemt layout, du kan printe igen hver uge. Vælg "Kun mig" for at beholde den selv, eller vælg en gruppe, så alle voksne i gruppen kan bruge og ændre den. På listen står gruppens navn efter navnet på en delt skabelon.',
          steps: {
            s1: 'Skriv et "Navn" under "Ny skabelon".',
            s2: 'Vælg "Kun mig" eller en gruppe under "Hvem kan bruge den".',
            s3: 'Klik på "Opret skabelon" for at åbne den i redigeringen.',
          },
        },
        templateRows: {
          title: 'Vælg, hvad planen viser',
          body: 'Vælg "Papir", hvilken dag planen "Starter normalt", og om du vil "Vis ugenummer". Tilføj derefter op til 12 rækker, fx "Måltid", "Aflevere / hente", "Arbejdssted", "Markér dage", "Begivenheder", "Tjekliste med opgaver" eller "Tom". Giv hver række en overskrift og en højde, og flyt den op eller ned. "Start fra eksempel" laver en plan, du kan rette til.',
        },
        colorsAndSaving: {
          title: 'Farver og gem',
          body: 'Under "Navnefarver" giver du hver forælder en farve, og under "Babysitterfarver" giver du babysitterne en farve i hente- og bringerækker. Klik på "Gem", når du er færdig. En skabelon henviser til dine data i stedet for at kopiere dem, så hvert print viser det aktuelle. En række, som den, der printer, ikke kan se, bliver printet som "Ikke tilgængelig".',
        },
      },
    },
    groupsAndSharing: {
      title: 'Grupper og deling',
      sections: {
        groups: {
          title: 'Hvad er en gruppe',
          body: 'En gruppe er familiens fælles rum: børnene, de voksne der hjælper, og de kalendere I deler. Alle kalendere hører til en gruppe. Hvert medlem er "Ejer", "Administrator" eller "Medlem". Ejere og administratorer kan invitere, tilføje børn og ændre tilladelser; kun ejeren kan slette gruppen. Du opretter grupper under Grupper i Indstillinger.',
        },
        addMembers: {
          title: 'Tilføj voksne og børn',
          body: 'Voksne bliver medlemmer via en invitation. Linket virker i 7 dage, og kun for en, der logger ind med den e-mailadresse og har bekræftet den. Børn skal ikke inviteres: tryk "Tilføj et barn" for at sætte et af dine egne børn direkte ind i gruppen.',
          steps: {
            s1: 'Under "Grupper" i Indstillinger trykker du "Inviter" ved gruppen.',
            s2: 'Skriv "E-mailadresse", og vælg en rolle.',
            s3: 'Tryk "Send invitation". Buddy sender linket på e-mail.',
            s4: 'Brug gerne "Kopiér link" eller "Del" til selv at sende linket.',
          },
        },
        permissions: {
          title: 'Hvem kan redigere, og hvem kan se',
          body: 'Under "Kalendertilladelser" vælger du for hver rolle, hvad gruppens kalendere tillader: "Ejer" kan redigere og styre, "Bidragyder" kan redigere begivenheder og opgaver, "Læser" kan kun se. Under "Måltidsplan-tilladelser" vælger du "Fuld adgang", "Kun læsning" eller "Ingen adgang". I en ny gruppe kan medlemmer se kalenderne, men ikke madplanen.',
        },
        guardianVsGroup: {
          title: 'Madplaner, medicin og værger',
          body: 'Madplanen hører til din familie; du deler den med en gruppe under "Del med en gruppe" i madplanen, og medicin fungerer på samme måde. Et medlemskab af en gruppe gør ikke nogen til værge for dit barn. Vil du have en anden voksen til at styre barnets konto sammen med dig, så brug "Inviter en medforælder" ved barnet under Børn i Indstillinger.',
        },
      },
    },
    admin: {
      title: 'Indstillinger',
      sections: {
        account: {
          title: 'Din profil og dine data',
          body: 'Under "Din profil" ændrer du navn, e-mail, tidszone og sprog; hver har sin egen gem-knap, fx "Gem sprog". "Download mine data" giver dig en JSON-fil med alt, hvad Buddy har om dig og dine børn. "Slet min konto" viser, hvad der ellers bliver slettet, før du bekræfter.',
        },
        children: {
          title: 'Giv et barn sit eget login',
          body: 'Har barnet glemt sin adgangskode? "Nulstil adgangskode" giver barnet en ny midlertidig og logger det ud. "Fjern" afbryder kun din egen forbindelse til barnet. "Slet" sletter barnet og alle barnets data og virker kun, når du er barnets eneste værge.',
          steps: {
            s1: 'Under "Børn" skriver du "Fornavn", "Efternavn" og "Login-brugernavn".',
            s2: 'Tryk "Tilføj barn".',
            s3: 'Kopiér den midlertidige adgangskode. Den vises kun én gang.',
            s4: 'Giv den til barnet, som vælger sin egen adgangskode første gang, det logger ind.',
          },
        },
        groupsAndCalendars: {
          title: 'Grupper og kalendere',
          body: 'Opret en gruppe under "Grupper", før du tilføjer kalendere. Under "Kalendere" tilføjer du en kalender til en gruppe, du ejer eller administrerer. Som kalenderens ejer kan du bruge "Abonnement" til en anden kalenderapp, "Flyt til gruppe", "Skift ikon" eller slette den. Se hjælpen om grupper og deling for invitationer og tilladelser.',
        },
        aiAssistant: {
          title: 'Brug din egen AI-nøgle',
          body: 'Madplanens AI-assistent bruger din egen API-nøgle fra Anthropic, OpenAI eller Google. Nøglen gemmes for hele familien, og den første nøgle, du tilføjer, bliver den aktive.',
          steps: {
            s1: 'Under "AI-madplansassistent" trykker du "Tilføj nøgle" ved udbyderen.',
            s2: 'Indsæt din API-nøgle, og tryk "Gem".',
            s3: 'Tryk "Test forbindelse" for at tjekke, at nøglen virker.',
            s4: 'Har du flere nøgler, trykker du "Gør aktiv" ved den, der skal bruges.',
          },
        },
      },
    },
  },
};
