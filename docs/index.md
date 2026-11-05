# Cadmus Index

The Cadmus index is a PostgreSQL database used to search items and their parts from the editor. It is derived data: it can always be rebuilt from the MongoDB data database (e.g. with the CLI `index` command).

It has two tables:

- `item`: the essential metadata of each item.
- `pin`: the data pins (name=value pairs) emitted by each part via `GetDataPins`. The framework knows nothing about the internal model of parts: pins are its only window into their content.

Packages:

- `Cadmus.Index`: storage-agnostic core: reader and writer interfaces, the query parser (`IndexQueryParser`) and its syntax tree, the `ItemIndexer` and the `ItemIndexFactory`.
- `Cadmus.Index.PgSql`: PostgreSQL implementation (plain SQL via Npgsql): `PgSqlItemIndexWriter` (tag `item-index-writer.pgsql`), `PgSqlItemIndexReader` (tag `item-index-reader.pgsql`), `PgSqlQueryBuilder`, `PgSqlIndexSchema`.
- `Cadmus.Index.Ef.PgSql`: ⚠️ deprecated shim, aliasing the PostgreSQL implementation under the legacy tags `item-index-writer.ef-pg` and `item-index-reader.ef-pg`. It will be removed in the next major version.

## Query Syntax

A query is made of **clauses** between square brackets, each with a name, an operator, and a value: e.g. `[title*=chronicle]`. Clauses can be combined with `AND`, `OR`, `NOT` (case-insensitive), and grouped with round brackets. `AND` takes precedence over `OR`; two consecutive clauses without a keyword are connected by `AND`. For example:

```txt
[title*=test] AND ([facet=default] OR [facet=tiled]) AND NOT [flags:to revise]
```

A query without any `[` is a shortcut for "title contains": `chronicle` is equal to `[title*=chronicle]`.

Anything else (e.g. text between clauses which is not a keyword or bracket) is a syntax error, reported with its position.

Limits: up to 64 clauses, and up to 16 levels of nested brackets.

### Names

Field names are case-insensitive:

| name                                | target                                     |
| ----------------------------------- | ------------------------------------------ |
| `title`, `t`                        | item title                                 |
| `description`, `dsc`                | item description                           |
| `facet`, `facetId`                  | item facet ID                              |
| `group`, `groupId`                  | item group ID                              |
| `sortKey`                           | item sort key                              |
| `flags`                             | item flags                                 |
| `itemId`                            | item ID                                    |
| `creatorId`                         | item creator ID                            |
| `userId`                            | item last user ID                          |
| `timeCreated`                       | item creation time                         |
| `timeModified`                      | item last modification time                |
| `partId`                            | part ID of the pin                         |
| `type`, `partType`, `partTypeId`    | part type ID of the pin                    |
| `role`, `roleId`                    | part role ID of the pin                    |
| `name`, `n`                         | pin name                                   |
| `value`, `v`                        | pin value                                  |
| `time`, `timeIndexed`               | pin indexing time                          |

Any other name is the **name of a pin**, matched against its value: e.g. `[eid=x12]` matches pins named `eid` with value `x12`. Pin names are case-sensitive.

To target a pin whose name is equal to a field alias (e.g. a pin named `title`, `type` or `name`), prefix it with `@`: `[@title=Chronicon]`.

### Operators

| operator | meaning                                                                       |
| -------- | ----------------------------------------------------------------------------- |
| `=`      | equal                                                                         |
| `<>`     | not equal                                                                     |
| `*=`     | contains                                                                      |
| `^=`     | starts with                                                                   |
| `$=`     | ends with                                                                     |
| `?=`     | wildcards: `?` = any single character, `*` = 0 or more characters             |
| `~=`     | regular expression (PostgreSQL POSIX syntax)                                  |
| `%=`     | fuzzy similarity (trigrams); append `:` and a threshold between 0 and 1 to the value (default 0.9), e.g. `[title%=chronicon:0.4]` |
| `==`     | numerically equal                                                             |
| `!=`     | numerically not equal                                                         |
| `<`      | numerically less than                                                         |
| `<=`     | numerically less than or equal                                                |
| `>`      | numerically greater than                                                      |
| `>=`     | numerically greater than or equal                                             |
| `:`      | flags: has any of the specified flags                                         |
| `&:`     | flags: has all the specified flags                                            |
| `!:`     | flags: has none of the specified flags                                        |

Text matching is case-sensitive. All the values are literals: e.g. `%` and `_` have no special meaning with `*=`.

Numeric operators applied to text (like pin values) consider only the values which are numbers (e.g. `[year>=1200]`); other values never match.

**Time fields** accept only comparison operators (`=`, `<>`, `==`, `!=`, `<`, `<=`, `>`, `>=`), with a date or date and time value, interpreted as UTC unless specified: e.g. `[timeModified>=2024-05-01]`.

**Flags** values are comma-separated lists of flags, each either a hexadecimal number (e.g. `10` = 16) or a flag label from the profile (case-insensitive): e.g. `[flags:to revise,4]`. Labels take precedence over hexadecimal numbers. With `flags`, `=` and `<>` compare with the whole set of flags (`[flags=3]`), while numeric operators compare the decimal flags value (`[flags>=8]`).

### Values

The value is everything after the operator up to the closing bracket, with leading and trailing spaces removed. Escapes:

- `\[` and `\]` for literal square brackets (e.g. in regular expressions: `[title~=^\[AB\]]`);
- `\\` for a literal backslash;
- `\HHHH` for any UTF-16 character, given its 4 hexadecimal digits code.

Any other backslash is kept as such: so, regular expressions like `\d+` can be written as they are.

### Items vs Pins Search

- **items** search (`api/search/items`) returns items sorted by sort key. Each clause on pins can be satisfied by **any** pin of the item, whatever the part providing it: e.g. `[tag=odd] AND [year==1200]` matches items having a pin `tag=odd` and a pin `year=1200`. Items without pins are found by clauses on item fields. `NOT` applied to a pin clause matches items not having any such pin.
- **pins** search (`api/search/pins`) returns pins sorted by name and value. All the clauses apply to the **same** pin (joined with its item for item fields): e.g. `[partTypeId=it.vedph.categories] AND [name=category] AND [value^=lit]` (as used by the editor for lookups).

The SQL code built for a query can be inspected with the CLI command `build-index-sql` (use `-p` for pins).

## Schema

The schema ([Schema.pgsql](../Cadmus.Index.PgSql/Assets/Schema.pgsql)) is designed for frequent small writes (each save indexes the saved item or part) and for search:

- each save deletes the pins of the saved part(s) by `part_id`, and inserts the new ones, in a single round trip and atomically. Writes to the same item are serialized by advisory locks, so that concurrent saves never duplicate pins.
- pin clauses are `EXISTS` subqueries resolved via `(name, value)` (equality and prefix) or via trigram indexes on `value` (contains, ends-with, wildcards, regular expressions, similarity).
- item search is sorted and paged via `(sort_key, id)`; title and description have trigram indexes.

The script is **idempotent**: it creates the schema in new databases, and upgrades existing databases. The API applies it at each startup (`HostSeedExtensions.EnsureIndexDatabaseAsync`), and the writer applies it once per process before its first write. It requires the `pg_trgm` extension (trusted since PostgreSQL 13, so the database owner can create it).
