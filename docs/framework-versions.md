# Majorsilence.Forms versions

`Directory.Packages.props` pins one exact published version (`MajorsilenceFormsVersion`). Each bump is its own commit and adds a
line here: the version, the date, what it unlocked, what it broke. CI never uses the framework checkout.

| Version | Date | Unlocked | Broke or still wrong | Shims removed |
|---|---|---|---|---|
| 26.3.0 | 2026-09-26 | The initial pin. Headless backend for UI tests, the Android and browser heads | Template Android head crashes (#288); text scale (#289); binding under trimming (#290); custom `OnPaint` units undocumented (#291) | none |

Released items: none

Implemented but not yet released: F1 (#263, PR #292), see `docs/framework-findings.md`. `tools/check-shims.sh` reads the "Released items:" line above.
