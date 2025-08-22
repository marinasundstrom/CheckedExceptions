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

## Testing
- Execute the unit tests with
  `dotnet test CheckedExceptions.sln`.

## Formatting
- Format each changed file using
  `dotnet format <path to dir of solution or project file> --include <comma separated list with file paths>`
  to respect `.editorconfig` rules.
