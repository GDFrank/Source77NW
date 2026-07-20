# Changelog

All notable changes to **Source77NW** are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Source tree reorganized for multiple library projects: library
  csprojs now sit side by side in `src/`, each compiling exactly one
  source subfolder (`Base/` today; `NT/`, `UI/`, `UI1/`... reserved as
  placeholders). The single project is renamed `Source77NW.Base` (was
  `LIBS.csproj`); a new `src/Directory.Build.props` turns off default
  globbing and splits `bin/`/`obj/` per project so side-by-side
  projects never collide. Root solutions: `LIBS.slnx` loads all
  library projects, `SAMPLES.slnx` all samples; `APPS.slnx` will join
  when the first app project arrives.

### Added
- Initial curation of the AppLab Core and Tools tiers: 28 source files,
  multi-targeting `net481` / `net8.0` / `net10.0` (C# 7.3 baseline).
- Repository scaffolding: license (MIT), CI build workflow, editor and
  git attribute configuration.
