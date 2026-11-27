# Flash Cards

Status: Implemented (issue #132). Two aggregates (`FlashCardSet` and `FlashCardPractice`), the
star award in `Progress`, privacy export and erasure, the `Features:FlashCards` flag (off by
default, and off when `Progress` is off), the guardian page `/guardian/flash-cards`, the child game
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

## Behavior

- The guardian makes sets in the library tab and, per child, switches a set on with its game lengths
  and reads the finished games (date, set, cards, known first time, stars).
- The child opens flash cards from Homework, picks a set (when there is more than one) and a game
  length, flips each card and answers _I knew it_ or _Not yet_. The result shows how many were known
  first time, the stars earned and the total.
- Playing the same length again the same day earns nothing; a longer one earns the difference.

## Testing

Backend integration tests per slice, golden-file event shape tests, snapshot tests, the award rule
and the finish replay and recovery paths; frontend unit specs for the game state machine, service,
child page and guardian components; `e2e/flash-cards.spec.ts` (a guardian makes a text and a maths
set and switches one on with two lengths; the child plays, misses a card, sees it again, gets
stars; the history row; same length again pays 0, a longer one the difference). The same-day steps
are skipped within five minutes of midnight in UTC or the device's zone.

## Estimate

|                  |                                                                                                                                                                                                             |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Complexity       | High -- two aggregates, a cross-aggregate star award with replay and recovery, a flag, privacy export and erasure, a generated deck, a guardian page with two tabs, a child game, about 120 en+da keys, e2e |
| Single developer | 3-4 weeks                                                                                                                                                                                                   |
| AI agent         | 9 slices over about 1 day of agent time, plus about 3 hours of human review (play it on a tablet, check the wording and the star values with a child)                                                       |
