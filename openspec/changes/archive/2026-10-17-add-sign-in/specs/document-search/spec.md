# Spec Delta

## MODIFIED Requirements

### Requirement: A search covers all of the owner's live documents and nobody else's
A search SHALL look at all of the caller's documents, in the inbox and in every archive. It SHALL NOT return a deleted document or a document of another owner.

#### Scenario: Inbox and archives together
- **WHEN** Alice has a document titled `Invoice March` in her inbox and one titled `Invoice April` in her archive `Taxes 2026`, and Alice's caller searches for `invoice`
- **THEN** the result contains both, and `Invoice April` carries the id of `Taxes 2026` as its archive

#### Scenario: Another user's documents
- **WHEN** Alice and Bob each have a document titled `Invoice` and Alice's caller searches for `invoice`
- **THEN** the result contains Alice's document only

#### Scenario: A deleted document
- **WHEN** Alice deletes her document titled `Invoice March` and then searches for `invoice`
- **THEN** the result does not contain it

#### Scenario: Unknown owner
- **WHEN** a client searches with a caller token naming a user id that belongs to no user
- **THEN** the request is refused as unauthenticated
