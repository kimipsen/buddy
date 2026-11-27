# Flash Cards Flow

A guardian makes card sets and switches them on per child with game lengths. The child plays a
game on the homework page and earns stars. Design: [flash-cards.md](../analysis/flash-cards.md).

Two aggregates, plus a write into `Progress`:

- `FlashCardSet` (schema `flashcards`): events `FlashCardSetCreated`, `FlashCardSetUpdated`,
  `FlashCardAdded`, `FlashCardUpdated`, `FlashCardRemoved`, `FlashCardSetDeleted`. A _Math_ set holds
  a `MathRule` and no cards; its deck is generated.
- `FlashCardPractice`: one stream per child (`FlashCardPracticeId.ForChild`), with
  `FlashCardPracticeStarted`, `FlashCardSetEnabled` (the tiers), `FlashCardSetDisabled` and
  `FlashCardGameFinished` (the results, the stars and the day).
- `ChildProgress` gets a `FlashCardStarsAwarded` event from `AwardFlashCardStars`.

```mermaid
sequenceDiagram
    actor Child
    participant API as Buddy API
    participant P as FlashCardPractice
    participant S as FlashCardSet
    participant G as ChildProgress

    Child->>API: GET /flash-cards/me/sets/{setId}/deck?tierId=...
    API-->>Child: the tier's cards (nothing stored)
    Note over Child: plays; the client makes a GameId
    Child->>API: POST /flash-cards/me/games {gameId, setId, tierId, results}
    API->>P: replay check by GameId (snapshot, then event query)
    API->>G: AwardFlashCardStars (tier stars minus paid today for the set)
    API->>P: FlashCardGameFinished (expected-version append)
    API-->>Child: stars, total stars, the game
```

## Endpoints

All routes are mapped only when `Features:FlashCards` is on (`404` otherwise).

| Method   | Route                                          | Who      | Behavior                                        |
| -------- | ---------------------------------------------- | -------- | ----------------------------------------------- |
| `GET`    | `/flash-cards/sets`                            | Guardian | The sets the guardian owns or shares            |
| `POST`   | `/flash-cards/sets`                            | Guardian | Create a text or math set                       |
| `PUT`    | `/flash-cards/sets/{setId}`                    | Guardian | Rename or change the math rule                  |
| `DELETE` | `/flash-cards/sets/{setId}`                    | Guardian | Soft delete                                     |
| `POST`   | `/flash-cards/sets/{setId}/cards`              | Guardian | Add a card to a text set                        |
| `PUT`    | `/flash-cards/sets/{setId}/cards/{cardId}`     | Guardian | Change a card                                   |
| `DELETE` | `/flash-cards/sets/{setId}/cards/{cardId}`     | Guardian | Remove a card                                   |
| `GET`    | `/flash-cards/children/{childId}`              | Guardian | What the child can practise, and finished games |
| `PUT`    | `/flash-cards/children/{childId}/sets/{setId}` | Guardian | Switch a set on with tiers (replaces the tiers) |
| `DELETE` | `/flash-cards/children/{childId}/sets/{setId}` | Guardian | Switch a set off                                |
| `GET`    | `/flash-cards/me/sets`                         | Child    | The sets the child can play                     |
| `GET`    | `/flash-cards/me/sets/{setId}/deck?tierId=`    | Child    | A deck of exactly the tier's card count         |
| `POST`   | `/flash-cards/me/games`                        | Child    | Finish a game; idempotent by `gameId`           |

Finishing returns `422` `game_id_reused` when a `gameId` belongs to another set or tier, and
`422` `game_no_longer_available` when the set or tier was switched off before any stars were paid.

## Validation

Every rule the endpoints enforce, validator rules first, then the handler's own checks.
A `400 validation_error` carries the message in the `ErrorEnvelope`'s `details` under the
key in the Field column, `""` for a check that isn't tied to one field (see
[HTTP status codes](../http-status-codes.md#error-response-shape-implemented)). A body
that can't be bound is rejected before these rules run, with the same envelope.

| Endpoint                                                                                         | Field                | Rule                                                                                                                 | Code                       | Status |
| ------------------------------------------------------------------------------------------------ | -------------------- | -------------------------------------------------------------------------------------------------------------------- | -------------------------- | ------ |
| `CreateFlashCardSet`, `UpdateFlashCardSet`                                                       | `name`               | Required: not empty. At most 100 characters.                                                                         | `validation_error`         | 400    |
| `CreateFlashCardSet`                                                                             | `kind`               | Must be a defined set kind (`Math` or `Text`).                                                                       | `validation_error`         | 400    |
| `CreateFlashCardSet`                                                                             | `kind`               | Cannot be `BuiltIn`: built-in sets are catalogue content, not created.                                               | `validation_error`         | 400    |
| `CreateFlashCardSet`                                                                             | `math`               | A math set needs a math rule; a text set cannot have one.                                                            | `validation_error`         | 400    |
| `CreateFlashCardSet`, `UpdateFlashCardSet`                                                       | `math`               | When present: a defined operation and a limit inside that operation's range.                                         | `validation_error`         | 400    |
| `UpdateFlashCardSet`                                                                             | `""`                 | Only a math set has a math rule.                                                                                     | `validation_error`         | 400    |
| `UpdateFlashCardSet`, `AddFlashCard`, `UpdateFlashCard`, `RemoveFlashCard`, `DeleteFlashCardSet` | `""`                 | Built-in sets are read-only.                                                                                         | `validation_error`         | 400    |
| `AddFlashCard`, `UpdateFlashCard`                                                                | `question`, `answer` | Required: not empty. At most 200 characters each.                                                                    | `validation_error`         | 400    |
| `AddFlashCard`                                                                                   | `""`                 | Cards can only be added to a text set.                                                                               | `validation_error`         | 400    |
| `AddFlashCard`                                                                                   | `""`                 | A set holds at most 200 cards.                                                                                       | `validation_error`         | 400    |
| `EnableFlashCardSet`                                                                             | `tiers`              | Between 1 and 5 tiers. Each tier needs a different card count. A tier id can only be used once.                      | `validation_error`         | 400    |
| `EnableFlashCardSet`                                                                             | `tiers[n].cardCount` | Between 3 and 50.                                                                                                    | `validation_error`         | 400    |
| `EnableFlashCardSet`                                                                             | `tiers[n].stars`     | Between 1 and 20.                                                                                                    | `validation_error`         | 400    |
| `EnableFlashCardSet`                                                                             | `""`                 | The set needs at least one card before it can be enabled.                                                            | `validation_error`         | 400    |
| `EnableFlashCardSet`                                                                             | `""`                 | A tier id must belong to the set's current tiers.                                                                    | `validation_error`         | 400    |
| `GetFlashCardDeck`                                                                               | `""`                 | The set must have cards to play.                                                                                     | `validation_error`         | 400    |
| `FinishFlashCardGame`                                                                            | `gameId`             | Not the empty id.                                                                                                    | `validation_error`         | 400    |
| `FinishFlashCardGame`                                                                            | `setId`, `tierId`    | Not the empty id.                                                                                                    | `validation_error`         | 400    |
| `FinishFlashCardGame`                                                                            | `results`            | Between 1 and 50 results. Slots 0 to the last one each appear exactly once.                                          | `validation_error`         | 400    |
| `FinishFlashCardGame`                                                                            | `results[n]`         | `cardKey`: required, at most 100 characters. `question` and `answer`, when sent: not empty, at most 200 characters.  | `validation_error`         | 400    |
| `FinishFlashCardGame`                                                                            | `results[n].cardKey` | The card key must belong to the set. A built-in set accepts only its catalogue keys; an unknown key is never echoed. | `validation_error`         | 400    |
| `FinishFlashCardGame`                                                                            | `""`                 | Send exactly one result for each card of the tier.                                                                   | `validation_error`         | 400    |
| `FinishFlashCardGame`                                                                            | `""`                 | The game id was already used for another set or tier.                                                                | `game_id_reused`           | 422    |
| `FinishFlashCardGame`                                                                            | `""`                 | The set or tier the stars come from is no longer enabled, so the game cannot be finished.                            | `game_no_longer_available` | 422    |

## Stars

`stars = max(0, tierStars - starsPaidTodayForThisSet)`, where "today" is the child's own day
(`User.TimeZoneId`). The server never reads a star value from the request. A finish is two writes
without a transaction (the award, then the game); a retry finds the award by `GameId` and writes only
the missing game (`flashcards.finish` outcome `recovered`, see [observability](../observability.md)).

## Privacy

The child's practice stream and flash-card stars are part of the child's data export and are erased
with the child. Sets belong to guardians.
