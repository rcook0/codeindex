# CodeIndex Golden Corpus (v2.1)

Deterministic inputs + expected outputs for API-compliant implementations.

## Layout
- inputs/     Source files (Java/C++)
- expected/   Expected SymbolIndex JSON outputs (schema_version = 2.1)
- profiles.*  LanguageProfile JSON used for the tests

## Behaviors covered
- stop words (reserved words excluded)
- line + block comments excluded
- string/char literal content excluded
- deterministic symbol sorting and occurrence sorting
