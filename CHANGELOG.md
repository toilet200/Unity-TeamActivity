# Changelog

## 2.2.0 - 2026-09-18

- Removed the project-specific legacy settings migration path.
- Kept project-scoped settings and storage as the only active configuration format.
- Generalized the package for reuse across unrelated Unity projects.

## 2.1.0 - 2026-09-14

- Improved Note input with Unity's standard search field behavior.
- Added stable Windows IME composition and Japanese text input.
- Added click-away focus handling for the Note field.
- Reduced unnecessary presence writes while typing.

## 2.0.0 - 2026-09-13

- Moved the menu to `DevTools > Team Activity`.
- Added stable project identification based on Unity's project `productGUID`.
- Isolated settings, client identifiers, and presence storage per project.
- Added project and device names to presence data and the Editor window.
- Added filtering so data from other projects cannot appear in the window.
- Added an embedded Unity Package manifest and Editor assembly definition.

## 1.0.0

- Added member presence, heartbeat status, Scene and asset activity, soft-lock warnings,
  Git status display, and the file-system transport abstraction.
