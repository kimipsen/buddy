export const help = {
  panelTitle: 'Help: {topic}',
  close: 'Close help',
  related: 'See also:',
  allTopics: 'All help topics',
  page: {
    backLink: 'Back to dashboard',
    eyebrow: 'Help',
    title: 'How Buddy works',
    intro:
      'Every help topic in one place. The "?" button at the top of a page opens the help for that page.',
    contentsLabel: 'Help topics',
    contact: 'Something not working? Ask whoever runs Buddy for your family, or see the project on',
    repositoryLink: 'GitHub',
  },
  topics: {
    dashboard: {
      title: 'Dashboard',
      link: 'Continue the guided setup, if you haven’t finished it',
      sections: {
        overview: {
          title: 'Your day at a glance',
          body: 'The dashboard shows today for your family: meals, tasks, events, medicine, pickups and your children. Each card links to its own page when you need more. Use "Print week plan" to print the week. If you put the guided setup on hold, a card with "Resume setup" lets you finish it.',
        },
        tasks: {
          title: "Tick off today's tasks",
          body: '"Today’s tasks" lists what is due today. Unfinished tasks whose time has passed move up under "Overdue". You can tick off a task that is assigned to you or to nobody. A routine from a template shows how many of its steps are done. "Today’s events" shows the rest of today\'s calendar, with past events crossed out.',
        },
        medicine: {
          title: 'Record medicine doses',
          body: 'Under "Today’s medicine" you see each dose with its time. Press "Mark taken" when the child has had it, or "Skip" if the dose is left out. Pressed the wrong one? "Undo" sets the dose back. To change the schedules themselves, use "Manage medicine →".',
        },
        mealsAndPickups: {
          title: 'Meals, pickups and stars',
          body: '"Today’s meal plan" shows breakfast, lunch, dinner and snack, and "Plan meals →" opens the meal planner. "Today’s pickup & drop-off" shows who brings and fetches each child; plan it with "Plan pickups →". The "Children" card lists your children with the stars they have earned towards their goals.',
        },
      },
    },
    calendar: {
      title: 'Calendars',
      sections: {
        views: {
          title: 'Day, week and month',
          body: 'Switch between "Day", "Work week", "Week" and "Month". "Week" shows seven days from the day you are on, and "Work week" shows Monday to Friday. Use "Today" and the previous and next buttons to move around. In "Month", pick a day to open it. With several calendars, the toggles under "Calendars" hide or show each one.',
        },
        ownership: {
          title: 'Who owns a calendar',
          body: "Every calendar belongs to a group; there are no private calendars. The group's calendar permissions decide what each member can do. By default the group owner can edit and manage the calendar, admins can edit it, and members can view it. A child sees the events and only the tasks assigned to them. You create calendars under Calendars in Settings.",
        },
        addItem: {
          title: 'Add an event or task',
          body: 'An event takes up time, such as a dentist visit. A task is something to do by a certain time, and can be ticked off when it is done. You can add to any calendar you can edit.',
          steps: {
            s1: 'Under "Add an event or task", choose the calendar and pick "Event" or "Task".',
            s2: 'Enter a title, and pick an icon and a colour if you like.',
            s3: 'Set the start and end, or the due date and time for a task, or turn on "All day".',
            s4: 'Use "Repeat" if it comes back. For a task, choose who it is for under "Assign to".',
            s5: 'Press "Add to calendar".',
          },
        },
        changeItems: {
          title: 'Change, delete or tick off',
          body: 'Each event and task has "Edit" and "Delete". "Edit" changes the title, icon, colour and times. "Delete" asks you to confirm first. Use a task\'s toggle to mark it done; for a repeating task this only marks that day. A routine from a template can only be deleted, and shows each step with its own toggle.',
        },
      },
    },
    taskLibrary: {
      title: 'Task library',
      sections: {
        templates: {
          title: 'Routines as templates',
          body: 'A task template is a reusable routine, such as getting ready for school, split into steps in a fixed order. Each step takes a set number of minutes, and the template shows the total time. Every template belongs to one child. If you have more than one child, choose the child at the top of the list first.',
        },
        buildTemplate: {
          title: 'Build a routine',
          body: 'Keep the steps short and concrete, so they are easy to follow one at a time.',
          steps: {
            s1: 'Enter a "Template name", pick an icon and a colour if you like, and press "Add template".',
            s2: 'Press "Edit subtasks" on the new template.',
            s3: 'For each step, enter a "Subtask title", set the minutes and press "Add subtask".',
            s4: 'Use the up and down arrows to change the order of the steps.',
          },
        },
        schedule: {
          title: 'Put a routine on the calendar',
          body: 'You schedule a template from the calendar page. The form shows how long the routine takes and when it ends, and each step appears in the calendar at its own time.',
          steps: {
            s1: 'Under "Add an event or task", choose "Task" and then "From template".',
            s2: 'Under "Assign to", choose the child the template belongs to.',
            s3: 'Pick the routine under "Task template".',
            s4: 'Set the "Due date" and the "Due time" when the routine starts, and press "Add to calendar".',
          },
        },
        archive: {
          title: 'Rename or retire a routine',
          body: 'Use "Rename" to change a template\'s name, icon or colour. When a routine is no longer used, press "Archive". It stays in the list marked "Archived", but you can no longer put it on the calendar.',
        },
      },
    },
    mealPlans: {
      title: 'Meal plans',
      sections: {
        planWeek: {
          title: "Plan the week's meals",
          body: 'Your meals live in one library that all your children share, so you plan each day only once. Under "Meals", type a name and press "Add meal". Buddy warns you if a similar meal already exists. "Archive" removes a meal you no longer make. Children can rate meals with stars, and their ratings appear under each planned meal.',
          steps: {
            s1: 'Under "This week\'s plan", pick a meal for breakfast, lunch, dinner or snack on any day.',
            s2: 'Drag a planned meal to move it, or to swap it with another day.',
            s3: 'Use "Next week →" and "← Previous week" to plan further ahead.',
          },
        },
        aiAssistant: {
          title: 'Let the AI assistant draft a plan',
          body: 'The assistant suggests meals from your own library. It needs an API key from an AI provider, which you add under Settings in "AI mealplan assistant". The first time, read what it shares and press "I understand, continue". You can limit it to meals the children have rated, or meals served in the last 30, 60 or 90 days.',
          steps: {
            s1: 'Press "Plan with AI" on the meal plan page.',
            s2: 'Choose the dates and meals to plan, then press "Start planning".',
            s3: 'Chat with the assistant until the draft plan looks right.',
            s4: 'Press "Apply to my meal plan", or "Discard" to throw the draft away.',
          },
        },
        importPlans: {
          title: 'Import older meal plans',
          body: 'If you kept meal plans in a note or a spreadsheet, you can bring them in. Paste the text or upload a text or CSV file. Nothing is saved until you have checked the result, and days you have already planned are kept. Under "Earlier imports", "Undo" removes the imported days that nobody has changed since.',
          steps: {
            s1: 'Press "Import older plans" on the meal plan page.',
            s2: 'Paste your text or upload a file, then press "Preview import".',
            s3: 'For each dish, use an existing meal, create a new one, or skip it.',
            s4: 'Press the import button, which shows how many days will be added.',
          },
        },
        calendarLink: {
          title: 'See meals in your calendar app',
          body: 'Under "Calendar subscription", press "Create subscription link" to get a private link to your meal plan. Copy it into your calendar app straight away, because Buddy shows it only once. Anyone with the link can see the meal plan. Press "Revoke" next to a link when you no longer want it to work.',
        },
      },
    },
    medicine: {
      title: 'Medicine',
      sections: {
        addSchedule: {
          title: 'Add a medicine schedule',
          body: 'A schedule says which medicine your child takes, how much, and at what times each day. If you have more than one child, choose the child first. Leave the end date empty if the medicine is ongoing.',
          steps: {
            s1: 'Enter the "Medicine name" and the dosage, for example 5ml.',
            s2: 'Set the first time under "Dose times".',
            s3: 'Choose a "Start date" and, if you like, an "End date (optional)".',
            s4: 'Press "Add schedule".',
          },
        },
        doseTimes: {
          title: 'Give a dose several times a day',
          body: 'Press "+ Add another time" to add each extra time of day the medicine is given, and "Remove" to drop one. The times repeat every day from the start date until the end date. When the medicine is no longer needed, press "Stop" next to the schedule and then "Confirm".',
        },
        doseStatus: {
          title: 'Mark a dose as taken or skipped',
          body: 'Today\'s doses appear on the dashboard under "Today\'s medicine". Press "Mark taken" when your child has had the dose, or "Skip" if it was left out. Pressed the wrong one? "Undo" sets the dose back to not done. Your child can also mark their own doses on their home page.',
        },
      },
    },
    sleepDiary: {
      title: 'Sleep diary',
      sections: {
        logNight: {
          title: 'Log a night',
          body: 'The diary has one row per night, with the same fields as a sleep clinic\'s form. Every field is optional. Buddy suggests the total time slept from your times, and you can change it. To fix a night later, press "Edit" in the list of nights, or "Clear this night" to remove it.',
          steps: {
            s1: 'Choose the date under "Night of".',
            s2: 'Fill in the bedtime, when your child fell asleep, wake-ups and naps.',
            s3: 'Press "Save night".',
          },
        },
        hygieneNotes: {
          title: 'Write down sleep routines',
          body: 'Under "Sleep hygiene measures", note the routines and rituals you use for the whole diary rather than one night, such as "no screens after 19:00". Press "Save notes". The doctor sees these notes together with the nights.',
        },
        shareWithDoctor: {
          title: 'Share the diary with a doctor',
          body: 'A share link lets a doctor read the diary without a Buddy account. They see 14 days at a time, always with your latest entries, and can print the page. Anyone with the link can read it, so press "Revoke" under "Active links" when it is no longer needed.',
          steps: {
            s1: 'Under "Share with a doctor", choose when the link expires, or "Never".',
            s2: 'Press "Create link".',
            s3: 'Copy the link right away, because it is only shown once, and send it to the doctor.',
          },
        },
      },
    },
    progress: {
      title: 'Progress',
      sections: {
        earnStars: {
          title: 'How your child earns stars',
          body: 'Your child earns a star each time a task assigned to them is marked done. For a task with subtasks, each subtask your child ticks off gives a star. If the task is marked not done again, the star is taken back. Your child sees their stars and their next goal on their home page.',
        },
        goalPosts: {
          title: "Set your child's goals",
          body: 'Goal posts are the star counts your child works towards, each with an icon and an optional label. Until you change them, Buddy uses 5, 10, 25, 50 and 100 stars. Each goal must need more stars than the one before.',
          steps: {
            s1: 'Choose the child, if you have more than one.',
            s2: 'Enter "Stars needed" and an icon for each goal.',
            s3: 'Press "+ Add another goal" for more, or "Remove" to drop one.',
            s4: 'Press "Save goal posts".',
          },
        },
        milestones: {
          title: 'Reaching a goal',
          body: "When your child's stars reach a goal post, they unlock that milestone, and its icon shows on their home page. Goals never run out: once your child passes the last one, new goals keep appearing with higher star counts, reusing your icons.",
        },
      },
    },
    pickup: {
      title: 'Pickup and drop-off',
      sections: {
        planSlot: {
          title: 'Plan a pickup or drop-off',
          body: 'The plan shows the next seven days, starting today, with a "Drop-off" and a "Pickup" for each day. If you have more than one child, choose the child at the top first. Every guardian of the child can edit the plan.',
          steps: {
            s1: 'Click "Not planned" on the day and slot you want.',
            s2: 'Choose who takes care of it.',
            s3: 'Add a time and notes if you like.',
            s4: 'Click "Save".',
          },
        },
        whoTakesCare: {
          title: 'Choose who takes care of it',
          body: '"A guardian" is one of the child\'s guardians. "A sibling" is one of your other children. "Goes alone" means the child manages without an adult. "Playdate" needs the host\'s name, and the location and contact info are optional. "Babysitter" lets you pick someone saved on the babysitter list of you or another of the child\'s guardians.',
        },
        changeOrClear: {
          title: 'Change or clear a plan',
          body: 'Click a planned slot to change it, or click "Clear" under it to set it back to "Not planned". "Not planned" means nobody has decided yet, while "Goes alone" is a deliberate choice that the child needs no escort that day.',
        },
        whereItShows: {
          title: 'Where the plan shows up',
          body: 'Today\'s plan appears on your dashboard under "Today’s pickup & drop-off". Your child sees their own pickups and drop-offs for today on their home screen, including a babysitter\'s name but never the contact info. A print template can also add the plan to the printed week plan.',
        },
      },
    },
    babysitters: {
      title: 'Babysitters',
      sections: {
        addBabysitter: {
          title: 'Save a babysitter',
          body: 'Save the babysitters and nannies who pick up or drop off your children once, and you can choose them whenever you plan a pickup. Contact info such as a phone number or email is optional.',
          steps: {
            s1: 'Type the babysitter\'s "Name".',
            s2: 'Add "Contact info (optional)" if you like.',
            s3: 'Click "Add babysitter".',
          },
        },
        useInPickups: {
          title: 'Use a babysitter in the pickup plan',
          body: 'In the pickup plan, choose "Babysitter" and pick a name. The list holds the babysitters saved by every guardian of the child, so you can use your co-guardian\'s babysitters and they can use yours. Your child sees the babysitter\'s name, never the contact info. In a print template you can give each babysitter a name color.',
        },
        editOrRemove: {
          title: 'Edit or remove a babysitter',
          body: 'Click "Edit" to change a name or contact info, or "Remove" to take someone off your list. You can only edit your own list. A removed babysitter can\'t be chosen for new pickups, but pickups already planned keep the name. To change such a pickup, choose someone else.',
        },
      },
    },
    workLocations: {
      title: 'Work locations',
      sections: {
        addLocations: {
          title: 'Add the places you work',
          body: 'Add each place you work, such as an office or another city, with an icon, a name and a color. Names must be unique. Only you and the other guardians of your children can see your work locations. Children never see them.',
          steps: {
            s1: 'Choose an "Icon".',
            s2: 'Type a "Name", such as Office.',
            s3: 'Pick a color.',
            s4: 'Click "Add location".',
          },
        },
        weeklyPattern: {
          title: 'Set your usual week',
          body: 'The weekly pattern says where you usually are on each weekday. If your weeks alternate, for example office one week and home the next, let the pattern repeat every 2 to 4 weeks. The weeks are named A, B and so on, and repeat in order.',
          steps: {
            s1: 'Under "Repeats every", choose how many weeks the pattern lasts.',
            s2: 'If it lasts more than one week, use "This week is" to say which week the current one is.',
            s3: 'Pick a location for each day you work.',
            s4: 'Click "Save pattern".',
          },
        },
        exceptions: {
          title: 'Change a single day or a holiday',
          body: 'Under "Exceptions", pick a location or "Off" for any day. The change is saved at once and wins over the pattern. Choose "Follow pattern" to undo it. Use "Earlier" and "Later" to move between weeks. For a holiday, use "Set a range" with "From" and "To" and click "Apply". A range can be at most a month.',
        },
        whyItMatters: {
          title: "What it's used for",
          body: 'Your work locations fill in the week plan you print. A "Work location" row in a print template shows where a guardian is each day, or marks the days they are at one chosen place. They don\'t appear in the calendar, the pickup plan or your child\'s app.',
        },
      },
    },
    print: {
      title: 'Printing week plans',
      sections: {
        printWeekPlan: {
          title: 'Print a week plan',
          body: 'A week plan covers seven days from the first day you choose, on A4 or A3 landscape paper as the template says. On the preview you can still change "First day" and turn on "Include subtasks". The app remembers the last template you used on this device.',
          steps: {
            s1: 'Under "Quick print", choose a "Template".',
            s2: 'Pick the "First day".',
            s3: 'Click "Preview".',
            s4: 'Click "Print".',
          },
        },
        createTemplate: {
          title: 'Create and share a template',
          body: 'A template is a saved layout you can print again every week. Choose "Just me" to keep it to yourself, or pick a group so every adult in that group can use and edit it. In the template list, a shared template shows the group\'s name after its own.',
          steps: {
            s1: 'Under "New template", type a "Name".',
            s2: 'Under "Who can use it", choose "Just me" or a group.',
            s3: 'Click "Create template" to open it in the editor.',
          },
        },
        templateRows: {
          title: 'Choose what the plan shows',
          body: 'Set the "Paper", which day it "Usually starts on" and whether to "Show week number". Then add up to 12 rows, such as "Meal", "Drop-off / pick-up", "Work location", "Mark days", "Events", "Chores checklist" or "Blank". Give each row a label and height, and move it up or down. "Start from example" fills in a plan you can adjust.',
        },
        colorsAndSaving: {
          title: 'Colors and saving',
          body: 'Under "Name colors", give each guardian a color, and under "Babysitter colors", give babysitters a color for pick-up rows. Click "Save" when you\'re done. A template refers to your data rather than copying it, so each print shows what\'s current. A row pointing at something the person printing can\'t see prints as "Not available".',
        },
      },
    },
    groupsAndSharing: {
      title: 'Groups and sharing',
      sections: {
        groups: {
          title: 'What a group is',
          body: 'A group is your family\'s shared space: the children, the adults who help, and the calendars you share. Every calendar belongs to a group. Each member is "Owner", "Admin" or "Member". Owners and admins can invite people, add children and change permissions; only the owner can delete the group. Create groups under Groups in Settings.',
        },
        addMembers: {
          title: 'Add adults and children',
          body: 'Adults join by invitation. The link works for 7 days, and only for someone who signs in with that email address and has verified it. Children don\'t need an invitation: press "Add a child" to put one of your own children straight into the group.',
          steps: {
            s1: 'Under "Groups" in Settings, press "Invite" on the group.',
            s2: 'Enter the "Email address" and choose a role.',
            s3: 'Press "Send invite". Buddy emails the link.',
            s4: 'If you like, use "Copy link" or "Share" to send the link yourself.',
          },
        },
        permissions: {
          title: 'Who can edit and who can view',
          body: 'Under "Calendar permissions" you choose, for each role, what the group\'s calendars allow: "Owner" can edit and manage, "Contributor" can edit events and tasks, "Viewer" can only view. Under "Meal plan permissions" you choose "Full access", "Read only" or "No access". In a new group, members can view the calendars but not the meal plan.',
        },
        guardianVsGroup: {
          title: 'Meal plans, medicine and guardians',
          body: 'Your meal plan belongs to your family; you share it with a group under "Share with a group" on the meal planner, and medicine works the same way. Joining a group does not make anyone your child\'s guardian. To let another adult manage your child\'s account with you, use "Invite a co-guardian" on the child under Children in Settings.',
        },
      },
    },
    admin: {
      title: 'Settings',
      sections: {
        account: {
          title: 'Your profile and your data',
          body: 'Under "Your profile" you change your name, email, time zone and language; each has its own save button, such as "Save language". "Download my data" gives you a JSON file of everything Buddy holds about you and your children. "Delete my account" shows what else will be deleted before you confirm.',
        },
        children: {
          title: 'Give a child their own login',
          body: 'Forgotten password? "Reset password" gives the child a new temporary one and signs them out. "Remove" only ends your own link to the child. "Delete" erases the child and all their data, and works only when you are their sole guardian.',
          steps: {
            s1: 'Under "Children", enter the "Given name", "Family name" and "Login username".',
            s2: 'Press "Add child".',
            s3: 'Copy the temporary password. It is shown only once.',
            s4: 'Give it to your child, who chooses their own password the first time they sign in.',
          },
        },
        groupsAndCalendars: {
          title: 'Groups and calendars',
          body: 'Create a group under "Groups" before you add calendars. Under "Calendars" you add a calendar to a group you own or administer. As the calendar\'s owner you can "Subscribe" from another calendar app, "Move to group", "Change icon" or delete it. See the groups and sharing help for invitations and permissions.',
        },
        aiAssistant: {
          title: 'Use your own AI key',
          body: "The meal plan's AI assistant uses your own API key from Anthropic, OpenAI or Google. The key is saved for your whole family, and the first key you add becomes the active one.",
          steps: {
            s1: 'Under "AI mealplan assistant", press "Add key" next to the provider.',
            s2: 'Paste your API key and press "Save".',
            s3: 'Press "Test connection" to check that the key works.',
            s4: 'With more than one key, press "Make active" on the one to use.',
          },
        },
      },
    },
  },
};
