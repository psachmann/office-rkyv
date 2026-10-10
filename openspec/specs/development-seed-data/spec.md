# development-seed-data Specification

## Purpose

Gives a developer a ready-to-use app on every fresh development database: the API server creates a demo user with an archive and inbox documents when it starts in Development, and never anywhere else.

## Requirements

### Requirement: The API server seeds demo data in Development
When the API server starts in the Development environment with seeding enabled, and the demo user does not exist, the system SHALL create a user with the email `demo@example.com` and the display name "Demo User", one archive named "Taxes 2026" owned by that user, and three documents owned by that user with the titles "Electricity bill March", "Rental contract" and "Insurance policy". The documents SHALL be in the user's inbox and the archive SHALL contain no documents.

#### Scenario: Seeding an empty database
- **WHEN** the seed runs against a database with no users
- **THEN** listing the users returns exactly one user, with the email `demo@example.com` and the display name "Demo User"
- **AND** listing that user's archives returns exactly one archive, named "Taxes 2026", with no documents
- **AND** that user's inbox lists exactly the three documents "Electricity bill March", "Rental contract" and "Insurance policy"

#### Scenario: Seeding beside existing data
- **WHEN** the seed runs against a database that has other users but not the demo user
- **THEN** the demo user, its archive and its three inbox documents are added, and the other users and their data are unchanged

### Requirement: Seeded data is ordinary data
Seeded users, archives and documents SHALL be subject to the same rules as data created through the API, and SHALL be readable, renamable, fileable and deletable through the API like any other.

#### Scenario: Working with a seeded document
- **WHEN** a client files the seeded document "Rental contract" into the seeded archive "Taxes 2026" through the API
- **THEN** the archive lists that document and the inbox no longer does

### Requirement: The seed runs once per database
The system SHALL treat the existence of the demo user as the sign that a database was already seeded. When the demo user exists, the seed SHALL add nothing: no user, no archive and no document, whatever has been changed or deleted since.

#### Scenario: The API restarts on the same database
- **WHEN** the seed runs a second time against a database it already seeded
- **THEN** there is still exactly one demo user, one archive and three documents

#### Scenario: Seeded data deleted by the developer stays deleted
- **WHEN** the seeded archive and one seeded document have been deleted, and the seed runs again
- **THEN** the archive and the document are not created again

### Requirement: Seeding happens only in Development and can be switched off
The system SHALL seed only when the environment is Development and the seeding setting is not switched off. The setting SHALL default to on in Development. Outside Development the system SHALL NOT seed, even when the setting is switched on.

#### Scenario: Development with the default setting
- **WHEN** the environment is Development and the seeding setting is absent
- **THEN** seeding is enabled

#### Scenario: Development with the setting switched off
- **WHEN** the environment is Development and the seeding setting is switched off
- **THEN** seeding is disabled

#### Scenario: Production with the setting switched on
- **WHEN** the environment is Production and the seeding setting is switched on
- **THEN** seeding is disabled

#### Scenario: The functional tests start empty
- **WHEN** the functional tests start the app
- **THEN** the landing page a test opens first lists no demo user

### Requirement: Only the API server seeds
Hosts of the API other than the API server SHALL NOT seed demo data. In particular the desktop app SHALL NOT, in any environment.

#### Scenario: The shared API composition does not seed
- **WHEN** the API is hosted through the shared composition alone, as the desktop app and the pipeline tests do, in the Development environment
- **THEN** a fresh database has no users after startup

### Requirement: A failed seed does not stop the API
When seeding fails, the system SHALL still start and serve requests, and SHALL report the failure in its log.

#### Scenario: The seed cannot create the demo user
- **WHEN** the seed runs and creating the demo user is refused
- **THEN** the seed ends without throwing, no archive or document is created, and a warning is logged
