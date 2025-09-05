# Configuration Guide

Choosing the right default classification determines how the analyzer treats exceptions that are not explicitly listed in the `exceptions` map.

## Default strategies

### `Ignored`
Use when you only want to analyze exceptions that are explicitly classified. Unlisted exceptions will be ignored entirely.

### `NonStrict`
The recommended starting point. Unlisted exceptions remain part of analysis and raise low-severity diagnostics (`THROW002` style) but do not require `[Throws]` declarations or `catch` blocks.

### `Strict`
Use for maximum enforcement. Any unlisted exception must be either declared with `[Throws]` or handled with a `catch` block; otherwise a high-severity diagnostic is reported.

## Tips
- Start with `NonStrict` to discover thrown exceptions without breaking builds.
- Move to `Strict` once you have classified all expected exceptions and want strong enforcement.
- Prefer `Ignored` only when you explicitly list every exception of interest or when onboarding gradually.

You can change the default via the `defaultExceptionClassification` setting and override individual types in the `exceptions` map.
