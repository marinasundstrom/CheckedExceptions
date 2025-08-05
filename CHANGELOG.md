
# Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

## [1.9.8] - 2025-08-06

### Fixed

- PR [#231](https://github.com/marinasundstrom/CheckedExceptions/pull/231) Detangle nested "Unreachable code detected" (`IDE001`)

## [1.9.7] - 2025-08-05

### Added

- PR [#226](https://github.com/marinasundstrom/CheckedExceptions/pull/226) Enable codefix "Remove redundant catch clause" for catch all
- PR [#229](https://github.com/marinasundstrom/CheckedExceptions/pull/229) Legacy redundancy checks

## [1.9.6] - 2025-08-05

### Added

- [#224](https://github.com/marinasundstrom/CheckedExceptions/issues/224) Update heuristics for accessor selection from XML docs

## [1.9.5] - 2025-08-05

Adding improvements to the control flow analysis that was introduced in the last release.

### Added

- [#212](https://github.com/marinasundstrom/CheckedExceptions/issues/212) Handle finally clause
- [#217](https://github.com/marinasundstrom/CheckedExceptions/issues/217) Remove unused diagnostic
- [#216](https://github.com/marinasundstrom/CheckedExceptions/issues/216) Assign categories to diagnostics
- PR [#222](https://github.com/marinasundstrom/CheckedExceptions/pull/222) Work on control flow in try/catch

### Fixed

- PR [#214](https://github.com/marinasundstrom/CheckedExceptions/pull/214) Fix handling of re-throws in catch and catch-all
- [#210](https://github.com/marinasundstrom/CheckedExceptions/issues/210) Exclude local func from unreachable code diagnostic
- [#221](https://github.com/marinasundstrom/CheckedExceptions/issues/221) Exclude stmts with contracts from Roslyn ctrl flow analysis

## [1.9.0] - 2025-08-04

Added new control flow analysis. This will be improved over the next releases.

### Added

- PR [#209](https://github.com/marinasundstrom/CheckedExceptions/pull/209) Improve code flow analysis

### Fixed

- PR [#206](https://github.com/marinasundstrom/CheckedExceptions/pull/206) Handle case for wrap in try/catch in expression bodied

## [1.8.5] - 2025-08-02

### Added

- PR [#203](https://github.com/marinasundstrom/CheckedExceptions/pull/203) Handle Cast expression

- PR [#205](https://github.com/marinasundstrom/CheckedExceptions/pull/205) Remove try when removing last catch using codefix

### Fixed

- PR [#201](https://github.com/marinasundstrom/CheckedExceptions/pull/201) Fix wrapping of expression bodies by try/catch

## [1.8.2] - 2025-08-01

### Added

- PR [#192](https://github.com/marinasundstrom/CheckedExceptions/pull/192) Improve redundancy analysis for try/catch

### Fixed 

- PR [#193](https://github.com/marinasundstrom/CheckedExceptions/pull/193) Fix inheritance analysis for virtual properties

## [1.8.1] - 2025-07-31

### Added

- PR [#191](https://github.com/marinasundstrom/CheckedExceptions/pull/191) Add support for Glob patterns in ignore list

## [1.8.0] - 2025-07-31

### Added

- PR [#177](https://github.com/marinasundstrom/CheckedExceptions/pull/177) Handle property decl in XML doc analyzer

- PR [#183](https://github.com/marinasundstrom/CheckedExceptions/pull/177) Ability to turn off XML doc interop feature

- PR [#184](https://github.com/marinasundstrom/CheckedExceptions/pull/184) Codefix for adding exceptions missing from base member

### Fixed

- PR [173](https://github.com/marinasundstrom/CheckedExceptions/pull/173) Handle inheritance in redundancy check

- PR [#179](https://github.com/marinasundstrom/CheckedExceptions/pull/179) Fix display format of exception names in diagnostics

- PR [#181](https://github.com/marinasundstrom/CheckedExceptions/pull/181) Improve robustness for redundancy check and inheritance

## [1.7.1] - 2025-07-30

### Fixed

- PR [#169](https://github.com/marinasundstrom/CheckedExceptions/pull/169) Handle local func and lambdas in redundancy check

## [1.7.0] - 2025-07-30

### Added

- PR [#161](https://github.com/marinasundstrom/CheckedExceptions/pull/161) Prepare for wrap strategies for "Surround with try/catch" fix

- PR [#162](https://github.com/marinasundstrom/CheckedExceptions/pull/162) Code fix that add exceptions from XML docs

- PR [#165](https://github.com/marinasundstrom/CheckedExceptions/pull/165) Warn when exception declaration is redundant

### Fixed

- PR [#166](https://github.com/marinasundstrom/CheckedExceptions/pull/166) Report diagnostics on name of declared type

- PR [#167](https://github.com/marinasundstrom/CheckedExceptions/pull/167) Show simple exception name in diagnostic

## [1.6.7] - 2025-07-29

### Added

- PR [#159](https://github.com/marinasundstrom/CheckedExceptions/pull/159) Fix formatting of promoted expression bodies

## [1.6.6] - 2025-07-29

### Fixed

- PR [#154](https://github.com/marinasundstrom/CheckedExceptions/pull/154) Fix promotion of expression bodies in codefix

## [1.6.5] - 2025-07-28

### Fixed

- PR [#150](https://github.com/marinasundstrom/CheckedExceptions/pull/150) Fix crash in "Add catch clause" 

## [1.6.4] - 2025-07-28

### Added

- PR [#147](https://github.com/marinasundstrom/CheckedExceptions/pull/147) Disallow prop decl with Throws and Get accessor

### Fixed

- PR [#148](https://github.com/marinasundstrom/CheckedExceptions/pull/148) Handle inheritance with props w Throws and get

## [1.6.3] - 2025-07-28

### Added

- PR [#144](https://github.com/marinasundstrom/CheckedExceptions/pull/144) Allow throws decl on expression-bodied props

### Fixed

- PR [#141](https://github.com/marinasundstrom/CheckedExceptions/pull/141) Apply Throws attribute to property with expr body

## [1.6.2] - 2025-07-28

### Added

- PR [#137](https://github.com/marinasundstrom/CheckedExceptions/pull/137) Promote from expression body to block

### Fixed

- PR [#135](https://github.com/marinasundstrom/CheckedExceptions/pull/135) Fix "Add catch" shouldn't be available in expression-bodies

## [1.6.1] - 2025-07-27

### Fixed

- PR [#131](https://github.com/marinasundstrom/CheckedExceptions/pull/131) Redo: Prevent catch clause fix in try-wrapped lambda

## [1.6.0] - 2025-07-27

### Added

- PR [#117](https://github.com/marinasundstrom/CheckedExceptions/pull/117) Handle redundant typed catch
- PR [#124](https://github.com/marinasundstrom/CheckedExceptions/pull/124) Code fix turns simple lambda into parameterized

### Fixed

- PR [#121](https://github.com/marinasundstrom/CheckedExceptions/pull/121) Remove leading trivia before adding ThrowsAttribute
- PR [#122](https://github.com/marinasundstrom/CheckedExceptions/pull/122) Prevent catch clause fix from appearing inside try-wrapped lambdas

## [1.5.2] - 2025-07-26

## Added

- PR [#113](https://github.com/marinasundstrom/CheckedExceptions/pull/113) Adapt code fix text to reflect how many exceptions affected

## Fixed

- PR [#110](https://github.com/marinasundstrom/CheckedExceptions/pull/110) Code fix not applicable to top-level statement

## [1.5.1] - 2025-07-26

### Fixed

- PR [#108](https://github.com/marinasundstrom/CheckedExceptions/pull/108) Fix crash on netstandard2.0

## [1.5.0] - 2025-07-26

### Fixed

- PR [#107](https://github.com/marinasundstrom/CheckedExceptions/pull/107) Diagnostics tied to `typeof` expression in Throws

## [1.4.3] - 2025-07-26

### Added

- PR [#104](https://github.com/marinasundstrom/CheckedExceptions/pull/104) Detect exception declarations redundant by type hierarchy

### Fixed

- PR [#102](https://github.com/marinasundstrom/CheckedExceptions/pull/99) Only display "Add catch clause" fix action when applicable

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