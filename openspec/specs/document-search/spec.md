# document-search Specification

## Purpose

Lets a user find their documents by words in a document's title, file name or the text of its file, wherever the document is filed. It covers what matches, which documents are searched, the order and the limit of the results, and which queries are refused.

## Requirements

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

### Requirement: Search results follow every change to a document
A document SHALL be found by its current title as soon as the call that created or renamed it has answered, and SHALL NOT be found by a title it no longer has. Documents that existed before search was introduced SHALL be found as well.

#### Scenario: Found right after the upload
- **WHEN** a client uploads `Invoice March.pdf` for Alice and, once the upload has answered, searches for `march`
- **THEN** the result contains the new document

#### Scenario: Found by the new title, not by the old one
- **WHEN** Alice renames her document `Invoice March` to `Electricity bill` and then searches for `invoice` and for `electricity`
- **THEN** the search for `invoice` does not contain it, and the search for `electricity` does

#### Scenario: Documents from before search
- **WHEN** a store holds documents that were created before search was introduced, and the application is started on it with search
- **THEN** a search finds those documents by their titles

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

### Requirement: Short and over-long queries are refused
The system SHALL ignore words of fewer than 3 characters in a query, and SHALL ignore a word that has no letter or digit. A query that leaves no word of 3 or more characters SHALL be refused as invalid on the field `query` with the code `too_short`. A query longer than 200 characters SHALL be refused on the same field with the code `too_long`.

#### Scenario: A query of two characters
- **WHEN** a client searches for `ab`
- **THEN** the call is refused as invalid with the field `query` and the code `too_short`

#### Scenario: An empty query
- **WHEN** a client searches for a query that is empty or only whitespace
- **THEN** the call is refused as invalid with the field `query` and the code `too_short`

#### Scenario: Short words next to a long one are ignored
- **WHEN** Alice has a document titled `Invoice 4711` and searches for `an 4711`
- **THEN** the result contains that document

#### Scenario: A query that is too long
- **WHEN** a client searches for a query of 201 characters
- **THEN** the call is refused as invalid with the field `query` and the code `too_long`

#### Scenario: Search syntax is taken literally
- **WHEN** Alice has a document titled `Invoice March` and searches for `invoice AND march`
- **THEN** the call is not refused and the result does not contain that document, because `AND` is searched for as a word and is in no title or file name

### Requirement: The search query stays out of telemetry
The words a user searched for SHALL NOT appear in any span or log the API emits for the search.

#### Scenario: No span carries the query
- **WHEN** a client searches for `Stromrechnung`
- **THEN** no span emitted while handling the call has an attribute or event containing `Stromrechnung`
