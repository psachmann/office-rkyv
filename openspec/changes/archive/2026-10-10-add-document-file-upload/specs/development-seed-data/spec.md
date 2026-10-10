# Spec Delta

## MODIFIED Requirements

### Requirement: The API server seeds demo data in Development
When the API server starts in the Development environment with seeding enabled, and the demo user does not exist, the system SHALL create a user with the email `demo@example.com` and the display name "Demo User", one archive named "Taxes 2026" owned by that user, and three documents owned by that user with the titles "Electricity bill March", "Rental contract" and "Insurance policy". Each of the three documents SHALL have a sample PDF file with the content type `application/pdf`. The documents SHALL be in the user's inbox and the archive SHALL contain no documents.

#### Scenario: Seeding an empty database
- **WHEN** the seed runs against a database with no users
- **THEN** listing the users returns exactly one user, with the email `demo@example.com` and the display name "Demo User"
- **AND** listing that user's archives returns exactly one archive, named "Taxes 2026", with no documents
- **AND** that user's inbox lists exactly the three documents "Electricity bill March", "Rental contract" and "Insurance policy"

#### Scenario: Seeding beside existing data
- **WHEN** the seed runs against a database that has other users but not the demo user
- **THEN** the demo user, its archive and its three inbox documents are added, and the other users and their data are unchanged

#### Scenario: Seeded documents have downloadable files
- **WHEN** the seed has run and a client downloads the file of the seeded document "Rental contract"
- **THEN** the download delivers a non-empty file whose size and hash equal the ones the document reports, and the document's content type is `application/pdf`
