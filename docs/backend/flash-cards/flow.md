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

## Stars

`stars = max(0, tierStars - starsPaidTodayForThisSet)`, where "today" is the child's own day
(`User.TimeZoneId`). The server never reads a star value from the request. A finish is two writes
without a transaction (the award, then the game); a retry finds the award by `GameId` and writes only
the missing game (`flashcards.finish` outcome `recovered`, see [observability](../observability.md)).

## Privacy

The child's practice stream and flash-card stars are part of the child's data export and are erased
with the child. Sets belong to guardians.
