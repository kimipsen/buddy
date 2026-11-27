# Flash Cards

Status: Implemented (issue #132). Two aggregates (`FlashCardSet` and `FlashCardPractice`), the
star award in `Progress`, privacy export and erasure, the `Features:FlashCards` flag (on by default
since #144, and off when `Progress` is off), the guardian page `/guardian/flash-cards`, the child game
`/child/homework/flash-cards`, an e2e spec and screenshots. See
[flash-cards/flow.md](../flash-cards/flow.md).

## Context

Short, repeated practice (times tables, spelling, vocabulary) suits children with ADHD when each
round is brief, has a visible end and pays off at once. Buddy already has stars and goal posts
([gamified-progress.md](gamified-progress.md)) and a homework page, so flash cards add a small game
on the homework page that pays stars.

## Decisions

| Question           | Decision                                                                                                          |
| ------------------ | ----------------------------------------------------------------------------------------------------------------- |
| Kinds of set       | _Text_ (a guardian types question and answer) and _Math_ (a `MathRule`; cards are generated and never stored)     |
| Who owns a set     | A guardian; a set is shared with the family's guardians. Deleting is soft: a started game can still be finished   |
| What a child plays | A set a guardian switched on for that child, in one of 1 to 5 tiers (3 to 50 cards, 1 to 20 stars)                |
| The game           | One pass over the deck, then each "Not yet" card once more. No timer. Only the first pass counts                  |
| Stars              | Paid once per set and day: a game pays `tier stars - stars already paid for that set today`, never less than zero |
| Trust              | The server computes stars from the stored tier; the client sends only per-card results and a client-made `GameId` |
| Replays            | A finish is idempotent by `GameId`; a retry returns the stored game, and a lost second write is recovered         |
| Built-in sets      | 16 read-only catalogue sets (clock, sight words, letters, English words, countries), see _Built-in sets_ below    |

## Behavior

- The guardian makes sets in the library tab and, per child, switches a set on with its game lengths
  and reads the finished games (date, set, cards, known first time, stars).
- The child opens flash cards from Homework, picks a set (when there is more than one) and a game
  length, flips each card and answers _I knew it_ or _Not yet_. The result shows how many were known
  first time, the stars earned and the total.
- Playing the same length again the same day earns nothing; a longer one earns the difference.

## Built-in sets

Alongside the sets a guardian makes, the app ships a catalogue of 16 ready-made sets (`FlashCardSetKind.BuiltIn`):
four clock levels (whole hours, half hours, quarters, every five minutes), four sight-word levels, two letter
levels (capitals, lowercase, with an example word per letter, Æ Ø Å included), three English levels and three
country levels (the Nordics, Europe, the world). The content lives in code (`Features/FlashCards/Types`), not in the
event store, so nothing is created, owned or erased per family and the catalogue is the same for everyone.

- **Identity.** Each set has a fixed id (UUIDv5, pinned by a test, never changed) and a stable `BuiltInKey`
  (`clock:1`, `countries:1`, ...). A card has a canonical key (`clock:3:30`, a country code, the word). Changing a key is a
  breaking change: a deliberate removal moves it to `BuiltInRetiredKeys`.
- **Read-only.** A guardian cannot create a `BuiltIn` set or change, add to, remove from or delete one (400
  `validation_error`; a child gets 403). A guardian can list the catalogue and switch a set on for a child with game
  lengths, exactly as for a made set. The library shows an example card per set.
- **Deck.** The deck is drawn from the catalogue with `Draw`: exactly `CardCount` cards, repeating cards when the set
  is smaller than the length (the set is switched on regardless).
- **Clock cards.** The front is an SVG clock face (`shared/clock-face`) whose accessible name describes the hand
  positions, never the time. The card's `Question` is therefore a neutral prompt and never contains the time
  words; the back is the Danish wording (`halv fire`, `kvart i fire`, `halv et`, `halv tolv`) from the tested
  `DanishClock` function.
- **Unknown-key policy.** On finish a catalogue key maps to the catalogue text. A key in `BuiltInRetiredKeys` keeps the
  echo of the client's text (a stale client still maps a card that was removed). A key in neither list, including a valid
  key of another built-in set, is rejected as `validation_error`, is never echoed, earns no stars and logs a warning
  (event id 16003: the set `BuiltInKey` and the slot, never the client's key). The golden retired list is empty at ship.
- **Metrics.** `flashcards.finish` gains a `kind` tag (`math`, `text`, `builtin`, `unknown`).
- **Languages.** Set names and card content are Danish in both UI languages; the child UI is the Danish-reading
  child's. The English UI shows English set names on the guardian and child pages, Danish names in history.

## Testing

Backend integration tests per slice, golden-file event shape tests, snapshot tests, the award rule
and the finish replay and recovery paths; frontend unit specs for the game state machine, service,
child page and guardian components; `e2e/flash-cards.spec.ts` (a guardian makes a text and a maths
set and switches one on with two lengths; the child plays, misses a card, sees it again, gets
stars; the history row; same length again pays 0, a longer one the difference) and a second test where a guardian
switches on a built-in clock set and country set and the child plays one game of each (the clock face has an
accessible name, the flag's back is a Danish name). The same-day steps are skipped within five minutes of
midnight in UTC or the device's zone. Built-in sets add catalogue content tests (unique canonical keys, counts per
level, 144 clock positions), the Danish clock-words table, the finish policy tests and `kind` metric tests.

## Estimate

|                  |                                                                                                                                                                                                             |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Complexity       | High -- two aggregates, a cross-aggregate star award with replay and recovery, a flag, privacy export and erasure, a generated deck, a guardian page with two tabs, a child game, about 120 en+da keys, e2e |
| Single developer | 3-4 weeks                                                                                                                                                                                                   |
| AI agent         | 9 slices over about 1 day of agent time, plus about 3 hours of human review (play it on a tablet, check the wording and the star values with a child)                                                       |
