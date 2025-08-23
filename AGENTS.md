# Agent Instructions

## Repository Overview
- Solution file: `CheckedExceptions.sln` in the repository root.
- Core analyzers and related projects:
  - `CheckedExceptions/` – main analyzer implementation.
  - `CheckedExceptions.Attribute/` – attributes consumed by the analyzer.
  - `CheckedExceptions.CodeFixes/` – code fixes accompanying the analyzer.
  - `CheckedExceptions.Package/` – packaging infrastructure.
- Tests:
  - `CheckedExceptions.Tests/` contains unit tests.
  - Additional sample and test projects live in `Test/`, `Test2/`, `SampleProject/`, `NetStandard2_0Test/`, and `NetStandard2_1Test/`.

## Build
- Restore and compile the solution with
  `dotnet build CheckedExceptions.sln`.

## Local Package Source
- `NuGet.config` includes `./artifacts/package/debug` for local packages, but it is currently unused.
- If a restore or build fails because this folder is missing, either create it (for example, `mkdir -p artifacts/package/debug`) or ignore the source.

## Testing
- Execute the unit tests with
  `dotnet test CheckedExceptions.sln`.

## Formatting
- Format each changed file using
  `dotnet format <path to dir of solution or project file> --no-restore --include <comma separated list with file paths>`
  to respect `.editorconfig` rules without triggering a restore.
