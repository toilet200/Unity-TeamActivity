# Team Activity

English | [日本語](README.ja.md)

A lightweight Unity Editor extension that shows what each team member is working on and warns about potential Scene, Prefab, and C# script conflicts before they become merge problems.

Team Activity uses advisory soft locks. It never makes project files read-only and never prevents another member from editing an asset.

<!--
Add a screenshot or animated GIF here.
Suggested location: Documentation~/Images/team-activity-window.png
-->

## Features

- Online, Idle, and Offline member status
- Active Scene display
- Automatically selected or manually pinned Prefab and C# script display
- Conflict Risk warnings when multiple members report the same Scene or asset
- Short shared notes for each member
- Optional display of locally modified Git files
- Project-specific settings and presence storage
- Separate identities for different computers used by the same member
- File-system transport that works with Dropbox, Google Drive, a NAS, or a network share
- Transport interface designed for future Supabase, Firebase, or custom backend support

## Requirements

- Unity 6000.0 or later
- A shared folder that every team member can access for multi-computer use
- Git installed and available locally only when Git change display is enabled

The package is Editor-only and does not add code to player builds.

## Installation

### Unity Package Manager

1. Open `Window > Package Manager` in Unity.
2. Click the `+` button.
3. Select `Install package from git URL...`.
4. Enter:

   ```text
   https://github.com/toilet200/Unity-TeamActivity.git
   ```

5. Open `DevTools > Team Activity` after Unity finishes importing the package.

### Embedded package

Alternatively, copy this repository into the target Unity project as:

```text
Packages/com.devtools.team-activity
```

## Team setup

1. Open `DevTools > Team Activity`.
2. Open `Settings`.
3. Enter a member name and a device name.
4. Choose the shared parent folder used by the team.
5. Click `Save settings and reconnect`.
6. Repeat the setup on every team member's computer.

Choose the shared parent folder, not the automatically generated project folder inside it. Team Activity creates and manages the project-specific folder automatically.

Example:

```text
TeamActivity/                         <- select this folder
└─ My Unity Project-a1b2c3d4/         <- created automatically
   ├─ <client-id>.presence.json
   └─ <client-id>.presence.json
```

The local path does not need to be identical on every computer. It only needs to point to the same synchronized or network-shared folder. Keep the folder outside the project's `Assets` directory.

The default `Library/TeamActivity` location is suitable for testing on one computer, but it is not shared with other computers.

## Using the window

### Automatic work item

When a Prefab or C# script is selected in the Project window, Team Activity reports it as the current work item.

### Pinned work item

Select an asset and click `Pin selection` to keep reporting it while selecting other objects. Click `Use automatic` to return to automatic selection tracking.

### Notes

Use the Note field for a short description such as `Adjusting boss attack timing`. Notes are shared with the same presence information.

### Conflict warnings

When two or more non-Offline members report the same Scene, Prefab, or C# script, the window displays a `Conflict Risk` warning. This warning is advisory only and does not lock or modify the asset.

## Presence status

| Status | Rule |
| --- | --- |
| Online | Heartbeat and tracked Editor activity are recent |
| Idle | Heartbeat is recent, but no tracked Editor activity has occurred for 2 minutes |
| Offline | No heartbeat has been received for 30 seconds |

Team Activity publishes a heartbeat every 5 seconds. A normal Unity shutdown immediately reports the member as Offline. A crash, network interruption, or delayed cloud sync is handled by the Offline timeout.

## Git change display

When `Show Git changes` is enabled, Team Activity periodically reads the local Git working-tree status and shares the changed file paths. The feature is optional. Team Activity continues working if Git is unavailable or the project is not a Git repository.

## Project separation

Team Activity reads Unity's stable project `productGUID` and creates a separate storage folder and Editor preferences for each project. Clones of the same Unity project share one presence group, while unrelated projects remain isolated even when they use the same shared parent folder.

## Storage and privacy

The default transport writes one small JSON presence file per client. Depending on current activity, it can contain:

- Project name and project identifier
- Member name and device name
- Client identifier
- Heartbeat and activity timestamps
- Active Scene path
- Selected or pinned asset path
- Note text
- Locally changed Git file paths

Team Activity does not upload data to its own server. When a cloud-synced folder is selected, data is handled by that folder's provider and its sharing permissions.

## Known limitations

- Cloud synchronization delays can temporarily make a member appear Offline.
- Soft locks are warnings, not exclusive file locks.
- Selection tracking reports supported project assets; it does not detect the active file inside an external code editor.
- Every member must configure access to the same shared folder.
- File-system synchronization is intended for small teams and lightweight presence data, not live collaborative Scene editing.

## Troubleshooting

### Other members do not appear

- Confirm that everyone selected the same shared folder.
- Confirm that the sync provider reports the project-specific folder as fully synchronized.
- Confirm that everyone opened a clone of the same Unity project.
- Click `Refresh` or `Save settings and reconnect`.

### A member briefly appears Offline

The shared-folder provider may be taking longer than the 30-second Offline timeout to synchronize the latest heartbeat file.

### Git changes are missing

Confirm that Git is installed, the Unity project is inside a Git repository, and `Show Git changes` is enabled.

## Backend replacement

`ITeamActivityTransport` separates presence storage from the Editor UI and activity detection. A Supabase, Firebase, or custom transport can replace `FileSystemTeamActivityTransport` without rewriting the window, status calculation, conflict detection, or Git integration.

## License

Team Activity is available under the [MIT License](LICENSE.md).

See [CHANGELOG.md](CHANGELOG.md) for version history.
