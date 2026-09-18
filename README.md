# Team Activity

Open **DevTools > Team Activity** in the Unity Editor.

## Team setup

1. Open `Settings` in the Team Activity window.
2. Set a member name and a device name.
3. Select the same network share or cloud-synced folder on every member's computer.
4. Save and reconnect.

The default directory is `Library/TeamActivity`, which is useful for a local test but is
not visible to other computers. Presence is a soft lock only: the extension shows a warning
and never blocks or modifies an asset.

Team Activity automatically creates a project-specific subfolder under the selected shared
directory. Unity's stable `productGUID` identifies the project, so cloned copies on different
computers share the same presence while unrelated projects remain isolated.

The window automatically reports the active Scene and a selected Prefab or C# script. Use
`Pin selection` when an asset should remain declared as the current work item while selecting
other objects. Git changes are optional and disappear automatically if Git is unavailable.

## Status rules

- Heartbeat: every 5 seconds
- Idle: no tracked editor activity for 2 minutes
- Offline: no heartbeat for 30 seconds

Closing Unity marks that member Offline. Reopening the project reuses the same entry rather
than creating a duplicate.

## Reusing the package

This directory is a Unity Package. It can be copied to another project's `Packages` directory
as `Packages/com.devtools.team-activity`, or installed from a Git repository using Unity Package
Manager. Each project receives separate Editor preferences, client identifiers, and storage.

## Backend replacement

`ITeamActivityTransport` is the boundary for presence storage. Implement it for Supabase,
Firebase, or another service, then construct that transport in `TeamActivityService.RecreateTransport`.
The window, activity detection, conflict detection, and Git integration need no changes.
