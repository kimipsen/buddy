# House Rules Flow

House rules are a family's written agreements ("Screen time", "Dinner", "Bedtime"), kept as short
rules with a markdown body. Children read them and tap "I've read this" for each new version.
Design: [house-rules.md](../analysis/house-rules.md). Guardians write and refine rules as drafts
the children can't see, then publish them: [house-rules-publishing.md](../analysis/house-rules-publishing.md).

One aggregate in the `houserules` schema:

- `RuleBook`: one stream per scope, either a child's personal rules or a household group's rules.
  `RuleBookId.Value` equals the scope's id (`ChildId.Value` or `GroupId.Value`), so there is no
  index document. A flat `ScopeKind` (`Child` | `Group`) is stored on the book, and a load whose
  kind doesn't match the route's kind counts as missing. The stream is created lazily by the first
  `AddRule`. A book holds an ordered list of at most 50 `Rule`s, drafts included, and an
  `(RuleId, child UserId) -> acknowledged revision` map. A `Rule` holds its published `Title`/`Body`
  (empty until first published, when `Revision` is `0`) and an optional `Draft` with the
  unpublished change.

The backend stores the markdown as plain text and never parses it. The frontend renders an
allow-listed subset through Angular templates (`shared/markdown-view`), never through `innerHTML`.

## Access

| Scope      | Tier        | Who                                    |
| ---------- | ----------- | -------------------------------------- |
| Child book | Manage      | An active guardian of the child        |
| Child book | Acknowledge | The child                              |
| Group book | Manage      | A group Owner/Admin who is not a child |
| Group book | Acknowledge | A child who is a group member          |
| Group book | View        | Any other group member                 |

Anyone else gets `404`. A caller who can see the book but tries to write gets `403`. Only Manage
sees drafts; every other reader sees the published rules only, as if the drafts didn't exist.

```mermaid
sequenceDiagram
    actor Guardian
    actor Child
    participant App as Client app
    participant API as Buddy API
    participant Rules as HouseRules feature
    participant Access as Guardian links / Groups
    participant Store as Rule book event store

    Guardian->>App: Add "Screen time" to the household
    App->>API: POST /house-rules/groups/{groupId}/rules
    API->>Rules: AddRule command (Group scope)
    Rules->>Access: Group snapshot + is the caller a child?
    Access-->>Rules: Owner/Admin, not a child (Manage)
    Rules->>Store: RuleBookStarted + RuleDraftAdded (first rule) or RuleDraftAdded
    API-->>App: 200 OK with the book; the rule is a draft the child can't see

    Guardian->>App: Publish "Screen time"
    App->>API: POST /house-rules/groups/{groupId}/rules/{ruleId}/publish
    API->>Rules: PublishRule command (Manage)
    Rules->>Store: RulePublished (revision 1)
    API-->>App: 200 OK with the book and each child's status

    Child->>App: Open my rules
    App->>API: GET /house-rules/children/{childId}
    API->>Rules: GetChildRules query
    Rules->>Store: Personal book snapshot
    Rules->>Access: The child's group memberships
    Rules->>Store: Each household book snapshot
    API-->>App: 200 OK, personal + households, PendingAcknowledgements

    Child->>App: "I've read this" (revision 1)
    App->>API: PUT /house-rules/groups/{groupId}/rules/{ruleId}/acknowledgement
    API->>Rules: AcknowledgeRule command
    Rules->>Store: RuleAcknowledged, unless already acknowledged
    API-->>App: 204, or 409 house_rule_revision_changed if the rule changed since it was read
```

## Endpoints

Every book-scoped route exists twice, once under `children/{childId}` and once under
`groups/{groupId}`. The group routes' operation ids end in `ForGroup` (`AddRuleForGroup`).

| Method   | Route                                | Tier                             | Behavior                                                                                                                                                                                                                                                                                                                                                    |
| -------- | ------------------------------------ | -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST`   | `.../rules`                          | Manage                           | Adds a rule at the end, as a draft. `{ title, body }`: title 1-100 characters, body 0-4000. The 51st rule (drafts count) is `400`. Returns the book.                                                                                                                                                                                                        |
| `PUT`    | `.../rules/{ruleId}`                 | Manage                           | Saves the rule's draft. `{ title, body, requireReacknowledgement = true }`, same limits as adding. `false` marks a small fix that keeps the children's acknowledgements once published (ignored on a first publish). The same content and switch as the draft appends nothing; the published text on a published rule discards the draft. Returns the book. |
| `POST`   | `.../rules/{ruleId}/publish`         | Manage                           | Publishes the rule's draft: `revision` + 1, and the children see it. No draft appends nothing. Returns the book.                                                                                                                                                                                                                                            |
| `POST`   | `.../rules/publish`                  | Manage                           | Publishes every draft in the book, in one append. No drafts appends nothing. Returns the book.                                                                                                                                                                                                                                                              |
| `DELETE` | `.../rules/{ruleId}/draft`           | Manage                           | Discards a published rule's draft. No draft appends nothing, `204`. A never-published rule is `400` (remove it instead).                                                                                                                                                                                                                                    |
| `DELETE` | `.../rules/{ruleId}`                 | Manage                           | Removes a rule, its draft and its acknowledgements, at once (removal isn't drafted). Idempotent: `204` for a rule that's already gone.                                                                                                                                                                                                                      |
| `PUT`    | `.../rules/order`                    | Manage                           | `{ newOrder }`: exactly the book's current rule ids (drafts included), each once, or `400`. Takes effect at once. The same order appends nothing. Returns the book.                                                                                                                                                                                         |
| `GET`    | `.../rules`                          | any                              | The book: `access` (the caller's tier), `children` and each rule with `isPublished`, `draft` and every child's status. Manage gets every rule with its draft; other tiers only published rules, without drafts. A child sees only their own status. A scope with no rules is an empty list.                                                                 |
| `PUT`    | `.../rules/{ruleId}/acknowledgement` | Acknowledge, or Manage on behalf | `{ revision, childId? }`. See below. `204`.                                                                                                                                                                                                                                                                                                                 |
| `GET`    | `/house-rules/children/{childId}`    | the child, or an active guardian | Everything the child is asked to keep, published rules only: the personal section (labelled with the child's given name), one section per household group the child is a member of (labelled with the group name, ordered by name), and `pendingAcknowledgements`. A guardian sees households whose group they don't belong to, read-only.                  |

`...` is `/house-rules/children/{childId}` or `/house-rules/groups/{groupId}`.

## Acknowledging

A rule has a `revision` (`+1` on every publish) and an `acknowledgementRevision` (the last revision
that asked children to read it again). A child is up to date when their acknowledged
revision is at least the rule's `acknowledgementRevision`.

| Request                                                                                     | Result                                                                              |
| ------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| A rule that has never been published                                                        | `404`                                                                               |
| `revision` greater than the rule's current revision                                         | `400`                                                                               |
| `revision` below `acknowledgementRevision` (a normal edit happened since the child read it) | `409 house_rule_revision_changed`. The client reloads and shows the new text first. |
| The child already acknowledged this revision or a later one                                 | `204`, nothing appended                                                             |
| Otherwise                                                                                   | `RuleAcknowledged` is appended, `204`                                               |

Who records it:

- The child acknowledges for themself. `childId` is left out, or names the child.
- A guardian with Manage may acknowledge for a child who can't read yet. `childId` is required (`400`
  without it). The child must be in the book's scope, and the caller must be an active guardian of
  that child, so a household admin can't tick for another family's child (`403`).
  `RuleAcknowledged.RecordedBy` names the guardian.
- An adult `View` member can't acknowledge (`403`).

## Events

`RuleBookStarted` (`ScopeKind`, `ScopeId`), `RuleDraftAdded` (`Content`), `RuleDraftEdited`
(`Before`/`After` content, `RequiresReacknowledgement`), `RuleDraftDiscarded` (`Discarded`),
`RulePublished` (`Content`, `Revision`, `RequiresReacknowledgement`), `RuleRemoved` (`Before`),
`RulesReordered` (`Before`/`After` ids), `RuleAcknowledged` (`ChildId`, `Revision`, `RecordedBy`).
`RuleAdded` and `RuleEdited` were written before drafts existed; they are no longer appended, and
replay as a rule added and published, and a published edit. Golden files:
`buddy.IntegrationTests/EventShapeTests/GoldenFiles/HouseRules/`.

## Personal data

- **Child erased.** Their personal book stream is deleted. Their acknowledgements in household
  books stay, keyed by a `UserId` that is a pseudonym once the user is erased. They are no longer a
  member, so they drop out of every status list.
- **Guardian erased.** The rules they wrote stay with the child or household. `AddedBy`/`EditedBy`
  are pseudonyms, as `CreatedBy` is on calendar items.
- **Group erased.** When an erasure leaves a group with nobody in it, `GroupsPersonalDataEraser`
  deletes the group. The house-rules eraser runs after it and deletes the book of every group that
  no longer exists. A group deleted through `DeleteGroup` keeps its unreachable book until that
  sweep next runs.
- **Export** (`houseRules` section): the personal books of the guardian's children and the book of
  every group they belong to, with acknowledgement status for their children.

## Feature flag

`Features:HouseRules` (default on). Off unmaps the `/house-rules` group. The data, the snapshot
projection and GDPR coverage stay.
