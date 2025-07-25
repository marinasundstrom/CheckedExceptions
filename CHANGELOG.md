
# Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

## [1.4.2] - 2025-07-25

### Added

- PR [#99](https://github.com/marinasundstrom/CheckedExceptions/pull/99) Generate descriptive variable names for catch clauses
- [#97](https://github.com/marinasundstrom/CheckedExceptions/issues/97) Add link to CHANGELOG to README

### Fixed
- PR [#100](https://github.com/marinasundstrom/CheckedExceptions/pull/100) Make "add try/catch" work on top-level statements

## [1.4.1] - 2025-07-25

### Added

- PR [#96](https://github.com/marinasundstrom/CheckedExceptions/pull/96) Add fixer that adds catch to existing try block

## [1.4.0] - 2025-07-24

### Fixed

- PR [#92](https://github.com/marinasundstrom/CheckedExceptions/pull/92) Improvements to code fixers
  - Rename code fixer actions
   - [#89](https://github.com/marinasundstrom/CheckedExceptions/issues/89) 
Fix handling of leading trivia in code fixer
   - [#93](https://github.com/marinasundstrom/CheckedExceptions/issues/93) 
Code fixer adds exception type to existing declaration 

## [1.3.6] - 2025-07-23

### Fixed

- PR [#88](https://github.com/marinasundstrom/CheckedExceptions/pull/88) Improvements to code fixers

## [1.3.5] - 2025-07-23

### Changed

- PR [#87](https://github.com/marinasundstrom/CheckedExceptions/pull/87)
  Change message for `THROW001`

## [1.3.1] - 2025-06-28

### Changed

- PR [#83](https://github.com/marinasundstrom/CheckedExceptions/issues/83)
  Improve source location for element access expression

## [1.3.0] - 2025-06-28

### Changed

- PR [#82](https://github.com/marinasundstrom/CheckedExceptions/pull/82) Report more precise source locations
- PR [#79](https://github.com/marinasundstrom/CheckedExceptions/pull/79) Handle implicit object creation expressions

### Fixed

- PR [#80](https://github.com/marinasundstrom/CheckedExceptions/pull/80) General exception error not on invocation expression

## [1.2.6] - 2025-04-12

### Added

* [#73](https://github.com/marinasundstrom/CheckedExceptions/issues/73) Inheriting or overriding member should have the same Throws declarations as overridden member"