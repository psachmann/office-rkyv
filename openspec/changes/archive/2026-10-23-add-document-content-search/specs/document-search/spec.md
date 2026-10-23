# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: A user's documents can be searched by title and file name`
- TO: `### Requirement: A user's documents can be searched by title, file name and text`

## MODIFIED Requirements

### Requirement: A user's documents can be searched by title, file name and text
The system SHALL find the documents of one owner in which every word of a query is found: in the title, in the file name, or in the text extracted from the document's file. A word SHALL match anywhere inside the title, the file name or the text, ignoring case. The words MAY match in any order, and different words MAY match in different places, including different pages of the text. A document whose file has no extracted text SHALL be searched by its title and file name only. The result SHALL include the same document facts as the inbox list, including the archive a document is in.

#### Scenario: A word inside a longer word
- **WHEN** Alice has a document titled `Stromrechnung März` and searches for `rechnung`
- **THEN** the result contains that document

#### Scenario: Case is ignored
- **WHEN** Alice has a document titled `Invoice March` and searches for `INVOICE`
- **THEN** the result contains that document

#### Scenario: Every word has to match
- **WHEN** Alice has documents titled `Invoice March` and `Invoice April` and searches for `invoice april`
- **THEN** the result contains `Invoice April` and not `Invoice March`

#### Scenario: Words in any order
- **WHEN** Alice has a document titled `Invoice March` and searches for `march invoice`
- **THEN** the result contains that document

#### Scenario: A match in the file name
- **WHEN** Alice has a document titled `Lease` whose file name is `mietvertrag-2026.pdf`, and searches for `mietvertrag`
- **THEN** the result contains that document

#### Scenario: One word in the title, one in the file name
- **WHEN** Alice has a document titled `Lease` whose file name is `mietvertrag-2026.pdf`, and searches for `lease 2026`
- **THEN** the result contains that document

#### Scenario: No match
- **WHEN** Alice searches for a word that is in none of her documents' titles, file names or texts
- **THEN** the result is empty and is not refused

#### Scenario: A word only in the text of a PDF
- **WHEN** Alice uploads a PDF titled `Scan` whose text layer reads `Zahnarztrechnung fuer Maerz 2026`, the extraction has been processed, and she searches for `zahnarzt`
- **THEN** the result contains that document

#### Scenario: A word only in the text of a scan
- **WHEN** Alice uploads an image titled `Foto` that OCR reads as `Kündigung des Mietvertrags zum 30. Juni`, the extraction has been processed, and she searches for `kündigung`
- **THEN** the result contains that document

#### Scenario: One word in the title, one in the text
- **WHEN** Alice has a document titled `Lease` whose text reads `Monthly rent of 950 euros`, and searches for `lease rent`
- **THEN** the result contains that document

#### Scenario: Words on different pages
- **WHEN** Alice has a two-page document whose first page reads `Invoice 4711` and whose second page reads `Payable within fourteen days`, and she searches for `4711 fourteen`
- **THEN** the result contains that document

#### Scenario: A word in none of the places
- **WHEN** Alice has a document titled `Lease` whose text reads `Monthly rent of 950 euros`, and she searches for `lease mortgage`
- **THEN** the result does not contain that document

#### Scenario: A file with no text
- **WHEN** Alice has a document whose file the extraction recorded as `unsupported`, titled `Archive`, and she searches for `archive`
- **THEN** the result contains that document

#### Scenario: Before the extraction has run
- **WHEN** Alice has uploaded a document titled `Invoice March` whose extraction has not completed, and she searches for `march`
- **THEN** the result contains that document

#### Scenario: Two documents with the same file
- **WHEN** Alice uploads the same PDF twice under the titles `Copy one` and `Copy two`, the extraction has been processed, and she searches for a word in its text
- **THEN** the result contains both documents

### Requirement: Results are ordered by relevance and limited
The results SHALL be ordered by where the query matched and then by how well. A document in whose title and file name together every word is found SHALL come before a document that is found only by also using its text. Among the first, the order SHALL be by how well they match, best first, and by title where two match equally well. Among the second, the order SHALL be by title. A search SHALL return at most 50 documents, counted over both groups, and SHALL say whether more documents matched than were returned.

#### Scenario: More than the limit
- **WHEN** Alice has 51 documents whose titles contain `invoice` and searches for `invoice`
- **THEN** the result holds 50 documents and says that more matched

#### Scenario: Within the limit
- **WHEN** Alice has 3 documents whose titles contain `invoice` and searches for `invoice`
- **THEN** the result holds those 3 documents and says that no more matched

#### Scenario: More than the limit, found by text
- **WHEN** Alice has 51 documents titled `Scan 01` to `Scan 51` whose texts contain `Kaufvertrag` and searches for `kaufvertrag`
- **THEN** the result holds 50 documents and says that more matched

#### Scenario: A closer match comes first
- **WHEN** Alice has documents titled `Invoice` and `Invoice for the repair of the garden fence, March` and searches for `invoice`
- **THEN** `Invoice` comes before the longer title

#### Scenario: A match in the title comes before a match in the text
- **WHEN** Alice has a document titled `Scan` whose text reads `Invoice 4711` and a document titled `Invoice for the repair of the garden fence, March`, and searches for `invoice`
- **THEN** the document titled `Invoice for the repair of the garden fence, March` comes before the document titled `Scan`

#### Scenario: Found by text, in title order
- **WHEN** Alice has documents titled `Scan B` and `Scan A`, both with the text `Kaufvertrag`, and searches for `kaufvertrag`
- **THEN** `Scan A` comes before `Scan B`

#### Scenario: A document is listed once
- **WHEN** Alice has a document titled `Invoice March` whose text also contains `invoice`, and searches for `invoice`
- **THEN** the result contains that document once
