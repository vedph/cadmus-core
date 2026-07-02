# Cadmus.Philology.Tools

This project contains various tools for working with philological data in Cadmus.

## Edit Operations

Components representing simple string editing operations, used by misspelling and potentially other Cadmus modules.

The purpose of such operations is to represent changes in a simple text (usually a single word form) in a structured way. For instance, in annotating an epigraphic Latin text with a word like `bixit` instead of `vixit` (hypercorrection due to spirantization of the bilabial plosive B), one could represent the details of the correspondance between these two forms with a single replacement operation, i.e. replace `b` with `v` at position 0 to get the corresponding normalized form. This is more structured and informative than a simple string diff, and can be used for various purposes, e.g. to generate statistics on the types of errors made by scribes.

### Diffing

Additionally, the system also provides logic for _generating_ (via diffing) the edit operations needed to transform one string into another. This is useful for automatically generating the edit operations needed to correct a misspelled word.

The idea is that in most cases automatic diffing provides the starting point for the operations; but then users can adjust them because all this is intended to represent changes in language over time. So, it's not a mechanic diffing, even though in many cases this is enough.

For instance, say I want to represent a change from `poena` to `pena` in Latin. Here the linguistic phenomenon behind the change is the monophthongization of `oe` into `e`. Now, a diffing would just produce a delete operation: delete `o` from `poena` to get `pena`. But this would not be as perspicuous as using a replace operation: replace `oe` with `e` In the former case, you see a deletion with no apparent reason; in the second case, even if you are not looking at the words, it's easy to understand that you are looking at a case of monophthongization.

Diffing has an optional adjustment behavior, enabled by default, which attempts to merge two atomic operations potentially representing a single higher-order operation. This happens essentially when:

- of two operations, one is delete and its deleted text is equal to the text being added by another insert or replace operation, AND there is no other insert or replace operation with the same text.
- optionally, limit the condition only to insert operations, excluding replace.

For instance, say that the two forms are `ABCDEF` and `AZCED`: here a normal diff produces 1=replace `B`=`Z`; 2=delete `D`; 3=replace `F`=`D`. Now, operation 2 (delete) deletes text `D`, which is equal to the text being added by operation 3 (replace). So, we could adjust operations as follows:

1. replace `B`=`Z` (unchanged).
2. move `D` on `F` (`D` is moved and takes the place of `F`).

If instead we chose to limit the condition to insert only, no adjustment would apply because 3 was a replace. If it was an insert, it could apply too.

### DSL

Edit operations are defined in a simple **DSL** and include both basic operations like insert and delete and higher level operations like replace, move, and swap.

Whatever the strategy, the operations with their DSL syntax are:

- delete `"A"@NxN!` (this is just a replace of A with zero)
- insert-before `@N+="X"`
- insert-after `@N=+"X"`
- replace `"A"@NxN="X"`
- move-before `"A"@N>@N`
- move-after `"A"@N->@N`
- swap `"A"@NxN<>"X"@NxN`

For the DSL syntax:

- `"A"` stands for the text value of the selected input in the operation. This is optional, but often added for clarity. When producing operations by diffing, you can choose whether to add this or not.
- `"X"` stands for the text value which is replacing the input or being added to the input or being swapped with the input.
- `@NxN` or just `@N` is the coordinates of the characters selected. The first `N` is the ordinal in the input (=A) word: 1=first character, 2=second, etc. The second `N` after the `x` (which can also be replaced by `×`) is the number of characters to select; when not specified, it defaults to 1. So, given an input form "FECIT", `@1`="F", `@3x2`="CI".
- after all the operations, we can add a free text note between `{}`: e.g. `"X"@1="V" {my note here}` and/or string tags separated by spaces and included in `[]`: e.g. `"X"@1="V" [tag1 tag2]`.
