# Tactical design — Members

Who is entitled to borrow, and until when. The subscription is the whole subject: when it starts,
when it lapses, what category it grants — never what a category may *do*, which is Circulation's
policy, merely indexed by a fact this context owns.

Boundaries come from [strategic-design.md](strategic-design.md). This document decides aggregates,
invariants and the moments where they meet.

## 1. What the business can change

| Setting | Value | Note |
|---|---|---|
| `MembershipDuration` | 12 months | The one real lever, see below |
| `CardNumber` length | 4 to 32 characters | A range, not a format, see §3 |

The duration is the one number staff could plausibly revise, and it lives in one named place for
that reason; the length bound is a convention, exactly as Holdings' two are. That is not enough to
earn a policy object — one lever is a setting, not a policy. The day categories subscribe
differently — a student membership aligned on the academic year is the ordinary case — the single
duration becomes a table indexed by category, an addition rather than a rewrite, the same reserve
Circulation's flat policy records for its own cap.

There is deliberately no `AgeOfMajority` setting. No rule in this context keys on an age
threshold, because category is a decision and not a derivation — §4 is the argument.

## 2. The aggregate

One aggregate, and one is the right number: nothing in this context spans two members. A household
enrolling together is several enrollments taken in one visit — §10 records the day that might
change.

```
Member
  MemberId          identity
  Name              given name and family name, in ordinary order, see below
  DateOfBirth
  Category          Adult | Child | Student — a decision, see §4
  CardNumber        unique in the library, see §3
  MembershipStart
  MembershipEnd
  ContactDetails    email, phone, postal address — each optional, see §5
  Guardian?         a name and a way to reach them, see §5
  ErasedOn?         the day this record stopped being a person, see below
```

**Invariants.**

* `MembershipEnd` is after `MembershipStart`.
* A `Child` member has a guardian, always — while the record is a person.
* `Name` and `CardNumber` are never blank — while the record is a person.
* `DateOfBirth` is in the past.
* An erased member acts no more: every operation refuses, and only the erasure itself answers a
  second request with success.

**Erasure is a terminal state, and the state is what relaxes the invariants.** The two qualified
rules above once read absolutely, and an absolute reading made the legal obligation
unimplementable: emptying a name violates *never blank*, removing a minor's guardian violates
*always*, and the aggregate refused the very operation the law requires. The resolution is
`Withdrawn`'s pattern on a person — a terminal state that changes what is true of the record.
Each identity invariant protects an act — being greeted, being reached, presenting a card — and
an erased member performs none of them: an invariant whose reason has ended ends with it. What
stays is what identifies nobody: the identifier every downstream context holds, the category, and
the membership span the statistics are argued from. The retired card's number must also stop
answering, which makes the uniqueness index a filtered one — erased members are legion in
waiting, and an unfiltered unique index would allow exactly one.

**Entitlement is computed, never stored.** There is no `Active | Expired` status field, and the
omission is the design. The passage of time is not an event — Circulation's daily run exists
because something has to *ask* — and a stored status would be false from midnight until a job
corrected it, silently, which is the `OnShelf` mistake transposed from space to time. The aggregate
stores only what it can vouch for, the two dates, and entitlement is judged against the clock at
the moment of asking. Which makes the clock injectable here for the same reason it is in
Circulation: none of this is testable against `DateTimeOffset.UtcNow`.

**The membership is the current period, not a history.** Renewal moves `MembershipEnd`; every
renewal is published, and a read model keeps the trail. The aggregate holds only what its
invariants govern — `HoldQueue` settled that rule on a hotter path than this one — and no invariant
here reads last year's dates.

**Entitlement constrains the act, not the state.** It is checked at every act that grants loan
time — a checkout, a renewal — and a membership lapsing mid-loan invalidates nothing: the borrower
was entitled when the period was granted, the due date stands, and a return is always accepted. The same shape as the cap of five — a rule about
what may be added, never a statement about what exists — and it spares the model a reconciliation
nobody asked for.

**A member is greeted, not filed.** `Name` is ordinary order — given name then family name, what
is printed on the card. `NameForm`, the filing order, stays in Catalog: a member record is not an
authority record, carries no variant forms, and nothing here files anyone under anything.

## 3. The card

What the member presents at the desk, identified by its number, unique in the library.

**It is not the identity.** `MemberId` is. Cards are lost, chewed, demagnetized and replaced as a
routine matter, and were the number the identity, a replacement would mint a second member and
orphan every loan the first one made — the exact argument that keeps `Barcode` from being a copy's
identity, and it transfers whole.

**The model does not parse it.** Cards are pre-printed in purchased batches, in whatever numbering
the supplier used, and two generations of them circulate at once. Length and non-blankness are the
whole of the validation, for Holdings' reason: a format rule rejects a legitimate card the day a
new batch arrives, at the desk, with a member waiting.

**Uniqueness is a set rule, not an invariant.** A `Member` cannot see the other members, so the
handler asks and a unique index in the module's schema is what actually holds. The handler's check
exists for the message — "this card is already assigned" — not for the guarantee.

A replaced number must also stop answering: a found card presented by someone else has to point at
nothing. The replacement event carries the previous number so a projection keyed on it can retract
it — `CopyRelabelled` already established the shape.

## 4. Age is a fact, category is a decision

`DateOfBirth` is recorded because staff record it and because the category is argued from it. No
rule derives one from the other: a member does not change category at midnight on their eighteenth
birthday. Category changes when a librarian changes it, ordinarily at renewal — the natural moment
the file is already open — and an automatic promotion would move a member out of `Child` while
their guardian is still the only way to reach them, a surprise nobody chose. `Condition` in
Holdings is the precedent: recorded because the decision is argued from it, deciding nothing by
itself.

The set is `Adult | Child | Student`, stored as its name — a human reads this table when something
looks wrong, and the rule that statuses humans read are stored as strings applies. It is an
enumeration today; the day the library invents a category — a *Senior*, a partner institution —
it becomes reference data, and §10 keeps the question open rather than pretending it is settled.

## 5. Contact, and the guardian

Every channel is optional, and a member with none is legitimate — not an edge case to be
validated away. The notification design already counts three outcomes, and *no channel available*
is the third: it surfaces a work list for staff to phone or write, and this context's job is to
make that state representable rather than to forbid it.

The guardian is who a minor is reached through: a name and contact details of its own, owned by
this context because a member has an identity and, separately, a way of being reached that may
belong to somebody else. It is **not a reference to another `Member`** — most guardians are not
members, and a link would make a parent's enrollment a precondition of a child's, which no library
asks for.

The French term is *représentant légal*, and it is wider than *parent* on purpose: a protected
adult under *tutelle* has one too. Which is why any category may carry a guardian and only `Child`
requires one — the invariant states the floor, not the ceiling.

## 6. Entitlement is the one question Circulation asks

The context map makes Members the supplier and Circulation the customer, and the contract is one
question: **may this person borrow, and as what?** Asked synchronously, at the desk, with the
member standing there.

```
membership current  → Entitled, and the category
membership lapsed   → Lapsed
no such member      → NoSuchMember
```

**It answers with entitlement and never with standing.** Whether a debt forbids the loan is
Circulation's judgement over an amount Charges states; whether the borrower is at the cap is
Circulation's own count. Members answering "may borrow" would fold both into the wrong context —
it answers for the subscription, which is the only thing it can vouch for.

`Lapsed` and `NoSuchMember` are different answers, not two spellings of *no*, for the reason the
lendability port distinguishes them: a lapsed membership opens a renewal conversation, an unknown
card means a mis-scan or a card from another library, and a librarian does different things with
the two.

On the far side, the anticorruption layer turns the answer into a `Borrower` — identity, category,
load, standing — and `BorrowerId` carries the **same** value as `MemberId`: the model is
translated, never the identity. Circulation asks at every act and caches nothing, so no event
subscription backs this contract; the day a `Borrower` projection wants to cache the category, the
events of §8 are already there to feed it.

## 7. The moments

### Enroll

*Inscription.* Identity recorded, category decided, guardian captured when the category requires
one, card issued, and the first membership period starts that day. Preconditions, cheapest first:

1. The card number is not already assigned (§3).
2. A `Child` enrollment names a guardian.

No precondition crosses a context boundary — the first module whose moments never leave it, which
is what being upstream of everything looks like. Eligibility — residency, the identity document
presented — is desk procedure, deliberately not a model fact: the model records who was enrolled,
not the paperwork that satisfied the librarian.

### Renew

Two arithmetics, chosen by the calendar:

* **Before expiry**: the new end is the old end plus the duration. Renewing early costs nothing,
  or members would learn to let memberships lapse before renewing — a rule teaching the behavior
  it exists to prevent.
* **After expiry**: the new end is today plus the duration. The gap is not billed and not
  back-dated; nobody was entitled during it, and the record should agree.

A member returning after three years **renews**; they do not re-enroll. `MemberId` persists and
the loan history with it — a re-enrollment would mint a second identity and orphan the first,
manufacturing exactly the duplicate that §10 worries about. Renewal is also the natural moment the
category is reviewed (§4).

Nothing refuses renewing twice in a morning, which buys two years. No rule the library has asked
for, and a paid subscription would make it a feature.

### Change category

A decision at the desk, any time — renewal is the ordinary moment, not the only one. Entering
`Child` requires a guardian on the record first, so the invariant holds at every instant; leaving
`Child` removes nothing, because changing what a member may do does not change who is reached — a
guardian outlives the category and is removed by its own operation, when the fact it records ends.

### Replace card

The old number retires, the new one is checked against the set (§3), and the event carries both.

### Update contact details, change guardian

Ordinary corrections, facts about how a person is reached. One guard: removing the guardian of a
`Child` is refused — the invariant again, and the operation that legitimately ends a guardianship
is the category change, not a deletion that would leave a minor unreachable.

### Erase

*Effacement.* The member asks to be forgotten, and the record empties while the identifier
stands. The moment §10 designed on paper, now a moment of the model: every personal field goes —
name, birth date, card, channels, guardian — the membership dates and category stay, and
`MemberErased` announces the identifier and nothing else, because an event reporting an erasure
must not itself carry what was erased. Whether an outstanding balance should stop the act stays a
decision at the desk (§10); a second request finds the work done and answers success. Afterwards
the entitlement port answers `NoSuchMember`: the identifier resolves to nobody, exactly as one
that never resolved at all — which keeps a found card or a stale screen from acting in a ghost's
name.

### Merge

*Fusion.* Two records turn out to be one person, and one of them becomes a pointer at the other.
Somebody enrolls twice under a maiden name, a card is reissued as a new record, a form is typed
twice on a busy afternoon — §10 has called this the data quality problem of every membership system
since before there was a moment for it.

**A desk act, and routed like one.** The person who notices is the member of staff with both files
open, and which record survives is their judgement — the one with the current address, the longer
history, the card in the person's hand. Nothing in the model can tell, so nothing here tries.

**A merge does not anonymize, and that is the difference from an erasure.** The absorbed record
keeps its name, its card and its channels: an audit has to be able to read which two records were
judged one person, and by which name. The card stops opening anything without being freed — the
entitlement port answers `NoSuchMember` for a merged record exactly as for an erased one, so every
checkout and every hold refuses it from the day of the merge, and the unique index keeps the number
reserved so nobody is issued it afresh.

**So erasure remains the one act a merged record still accepts.** Every other operation refuses:
the person is over there now, act there. This one does not, because the merge left personal data
where it was, and a person's right to be forgotten does not stop at the record somebody judged the
secondary one. Refusing would leave exactly the data that was asked to be removed, in the row
nobody is allowed to touch.

**And two refusals, not one.** A record that no longer acts says *why* — `MemberErased` is the end
of a relationship and there is nowhere to go, `MemberMerged` means the person is here under the
other record. Folding them into one code would throw away the only part a member of staff can act
on.

The rules of the pair — a record cannot absorb itself, neither side may already be merged, and
neither may be erased, since an anonymized record leaves nothing by which anyone could have judged
it a duplicate — live together in `MemberMergeDomainService`, and `MergeInto` is internal behind it.
That is the arrangement Catalog's merge settled and Circulation's queues repeated.

### Rename

One operation, not two. Catalog needs `Rename` and `CorrectPreferredName` because a variant name
survives the first and not the second; a member has no variant forms and no access points, so the
distinction buys nothing here, and one operation carries the marriage and the typo alike.

## 8. Events

Facts about people and their subscriptions. Every one of them feeds a projection and nothing else
today, and they are published all the same, for the reason Holdings publishes its stream: an event
not published when it happened cannot be recovered afterwards, and the member file staff consult
is a read model fed by exactly this history.

**Published.**

| Event | Consumed by |
|---|---|
| `MemberEnrolled(memberId, category, cardNumber)` | read model |
| `MembershipRenewed(…, newEnd)` | read model |
| `MemberCategoryChanged(…, previousCategory, newCategory)` | read model |
| `CardReplaced(…, previousCardNumber, newCardNumber)` | read model |
| `ContactDetailsChanged(…)` | read model |
| `GuardianChanged(…)` | read model |
| `MemberRenamed(…, previousName, newName)` | read model |
| `MemberErased(memberId)` | read model — which owes its own copies the same emptying |
| `MembersMerged(absorbedMemberId, survivingMemberId)` | read model, **and the modules downstream** |

`MembersMerged` is **the first thing this context announces to another**, and it took a merge to
produce one: Members has been asked questions and has answered them through `IMemberEntitlement`,
and has stated nothing on its own initiative until now. It had to become an announcement because no
database constraint crosses a schema — nothing else can carry the news that an identifier
Circulation and Charges are holding has stopped meaning what it meant.

The contract is this context's own and not Catalog's, though `EditionsMerged` carries the same two
shapes: they are published by different modules, consumed by different subscribers, and mean
different things, and one shared record would make the two change together forever. It carries two
identifiers and nothing else — a contract offering a name would put a person's name into two more
schemas, which is precisely what this boundary bought by refusing it.

**Consumed: nothing.** No context in the map is upstream of Members, so there is no table of
consumed events to write — a first. Notifications reaches a member's address by query, the dashed
arrow of the context map, not by subscribing to this stream.

## 9. Deliberately left out

**Expiry reminders.** *Your membership lapses in two weeks* is a courtesy message, and it waits
for Notifications to exist. When it lands it is a daily query owned by this context — deciding a
membership is about to lapse is a Members fact — and its idempotence will rest on a
`RemindersSent` set on the member, the lesson Circulation's scheduled process already recorded.
Named now so it arrives as an addition, not a redesign.

**Membership fees.** The membership is free, by the assumption a municipal library usually
satisfies. A paid subscription would be a Charges fact created when Members announces an
enrollment or a renewal — Members publishes the fact, Charges prices it, the `LoanReturned` shape
exactly. Named so that pricing memberships one day is a decision, not a discovery.

**Self-service.** The vision settles it: the system serves the staff, and members are described by
it, not users of it. A member portal would be a different system reading this one's projections.

**A behavioral block.** A member banned for conduct is not a Members status: `Standing` is
Circulation's judgement, and the strategic design already holds the question open as one more
input to that judgement. Adding a `Suspended` here would put the verdict in the context that keeps
the facts.

## 10. Consequences and open questions

**What building the merge added, and it was one question this document had answered the other way.**
The two terminal states look interchangeable — a record that stops acting, an identifier that stops
resolving — and treating them so was the natural mistake. Erasure and a merge differ in what they
leave behind: an erasure empties the row, a merge does not. Everything else follows from that one
asymmetry. The refusal codes have to differ, because the member of staff reading them acts
differently. The rules of the merge itself have to refuse an erased record on either side, because
an anonymized row leaves nothing by which anyone could have judged it a duplicate. And erasure has
to remain reachable *through* the merge, because the absorbed record still holds a person's name
and address and a right does not stop at the file somebody judged secondary.

**The port that had to learn nothing is the part worth noticing.** Keeping new loans and holds off
an absorbed identifier cost one clause in `MemberEntitlement` — a merged record answers
`NoSuchMember` exactly as an erased one does — because Circulation already asks this question
before every checkout and every hold. Catalog's merge needed the same guard on its own side and got
it the same way. A port that answers *is this thing still a thing* pays for itself the second time
a terminal state is added, and this was the second time.

**What building it added.** Three things the design did not anticipate and the code settled, all
in the same corner: this is the first module whose values have parts — a name of two, a guardian
of a name and channels — where every earlier module's values were single scalars.

A composite value cannot arrive through a constructor. The store binds only scalar-mapped
properties to constructor parameters, so the aggregate's constructor takes the scalars and the
name, the channels and the guardian arrive through their setters — one construction path, used by
`Enroll` and the materializer alike, rather than a second constructor kept for the store's
benefit.

An optional composite needs a presence column. With every guardian column nullable, the store
cannot tell "no guardian" from a guardian it never heard about; the guardian's direct members are
themselves values, so none of their columns can discriminate alone. `Guardian_Present` is that
one bit, engine-managed, named for the question it answers.

And the outbox now holds its first object-shaped payloads: a name, channels and a guardian
serialize as objects, not strings, because flattening a value with parts invents a syntax someone
would eventually parse back. Their property names are contract exactly as an event record's
positional parameters are — renaming a part is the same decision as renaming a parameter, and
[outbox.md](outbox.md) §3's rule now reaches one level further down.

**Erasing a member empties the record and keeps the identifier — built, and §2 and §7 now carry
it.** Data-protection law gives a member the right to be forgotten, and the shape of the answer
needed no lawyer — only an inventory of where a person actually appears. Building it settled what
the paper design had missed: the identity invariants read absolutely and refused the emptying, so
erasure became a terminal state that relaxes them (§2), and the card index became filtered so two
erased members can coexist — an accident that would otherwise have surfaced at the second erasure
ever run in production.

They appear in two places. The `Member` aggregate holds the whole of it: the name, the date of
birth, the channels, the guardian, the card. And every downstream context holds a `Guid` and nothing
else — Circulation refuses the address by design, Charges keys an account by the same identifier,
and `CreatedBy` names the *employee* who acted and never the member, which is where §6's insistence
on that distinction quietly pays for itself.

So erasure clears the aggregate's personal fields and leaves `MemberId` standing. **No downstream
context changes at all**: their identifier stops resolving to a person, which is what anonymization
means, and the loans and charges that reference it stay countable. Deleting the member instead would
orphan every one of those references — the merged-edition problem Holdings §10 records, manufactured
on purpose.

The one copy this misses is the outbox. `MemberRenamed` carries both names, `ContactDetailsChanged`
the channels, `GuardianChanged` a whole guardian, and those payloads sit in this module's own table
after they are delivered. Emptying the aggregate and leaving them would be erasure in name only, and
[outbox.md](outbox.md) §10 carries the purge that bounds them — deferred as a storage convenience,
found to be a requirement here, and now a mechanism every module is wired with rather than a policy a
host might forget. This module has no projection
today, so those rows are the *only* second copy; a projection added later inherits the same duty.

**A member who still owes money is a decision at the desk, not a rule in the model.** The balance is
on the screen, the librarian is the one holding the request, and §7 already draws that line for
eligibility: the model records who was enrolled, not the paperwork that satisfied the librarian.
Asking Charges before erasing would add a synchronous edge the context map does not carry, on a path
walked a few times a year.

**What publishing the module's first query settled: an erased record answers.** `GetMemberByIdQuery`
exists so the desk's member file can ask who the person is, and building it forced a question the
erasure work had not reached. Every *command* refuses an erased member, and the obvious symmetry
would have had the read refuse too — `MemberErased` is already a code this module owns. It is the
wrong symmetry. A command refuses because acting on a record that is no longer a person is what
erasure forbids; a read changes nothing, so it can break nothing, and the desk has a real question:
the loans and charges keyed to that identifier stay countable after the person is gone, and a
librarian holding one of them has to be told *why* the name is missing. So the query succeeds and
carries `ErasedOn`, with the personal fields null because they are genuinely gone. Putting that
fact in an error would have made history read as a fault.

Two smaller things the answer's shape decided. The date of birth is not on it — it exists to hold
one invariant, a child has a guardian, and a screen that displayed it every time a file opened
would spread the most sensitive field on the row further than the rule that needs it. And the
category travels as its name, for the reason the column already stores it that way.

Open, and each deferred for a stated reason rather than forgotten:

* **Erasure**, whose modeling half is settled above and whose remainder is legal: how long a lapsed
  membership is kept at all, and whether a claim for money is a legitimate ground to keep a person's
  record past a request to erase it. Neither is a question about aggregates, and neither is
  answerable until someone who knows the law is in the room.
* **Duplicates.** The same person enrolled twice under slightly different names is the ordinary
  data quality problem of every membership system, and **this context's half of it is now built**
  (§7). A merge orphans one `MemberId` downstream — the exact shape of the merged-edition problem
  Holdings §10 recorded against Catalog — so what this context publishes is an event its downstream
  consumes rather than an update, as
  [ADR-0017](adr/0017-a-merge-is-an-event-and-circulation-pays-for-it.md) settles.
  **Circulation's consuming half is now built**: it repoints the loans of the absorbed record and
  combines that borrower's claims in every queue — the same wall a merged edition hits, reached
  from the other side, on machinery that was already there. One correction the building made to
  this paragraph as first written: *live* loans undersold the scope. A loan's borrower is asked
  about until the loan is answered for — a written-off loan still speaks its `BorrowerId` the day
  its copy resurfaces — so the sweep is wider than the edition merge's, and the cut is argued in
  the Circulation document's §10. What stays open is **Charges'** share: the surviving account
  absorbing the other's outstanding charges, which that context decides rather than this record.
  One consequence falls out for free and is worth naming: the survivor's balance moves, Charges
  announces it, and Circulation already judges that pair against its threshold.

  The two questions recorded here for Circulation's half were answered as anticipated. **The cap
  of five** exceeded by a merge needed no code at all: the cap constrains the *act*, so the
  survivor simply borrows nothing more until enough drains. **And one queue holding two claims of
  what turns out to be one person** is resolved by the edition merge's rules read with the words
  swapped — a claim awaiting pickup always survives, the earliest queued claim survives, and the
  rule turned out to live in the queue itself rather than in a fourth domain service, for the
  reason the Circulation document's §10 records.
* **Households.** One adult, three children, one visit, one payment someday. Today that is four
  members and a guardian repeated; a `Household` grouping earns its place the day the library
  wants family cards or a single renewal for all four.
* **Categories as data.** The enumeration becomes reference data the day a category is invented by
  the library rather than by the model (§4).
* **Whether the entitlement answer should carry `MembershipEnd`**, so Circulation could cap a due
  date at expiry. Today it deliberately does not: a loan outliving its membership is the decided
  behavior (§2), and publishing the date would invite the cap nobody asked for.
