using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DevTools.TeamActivity
{
    internal sealed class TeamActivityWindow : EditorWindow
    {
        private const double NotePublishDelaySeconds = 0.3;

        private Vector2 scrollPosition;
        private bool showSettings;
        private string memberName;
        private string deviceName;
        private string sharedDirectory;
        private string note;
        private bool notePublishPending;
        private double notePublishAt;
        private Rect noteFieldRect;
        private SearchField noteSearchField;

        [MenuItem("DevTools/Team Activity")]
        private static void Open()
        {
            var window = GetWindow<TeamActivityWindow>();
            window.titleContent = new GUIContent("Team Activity");
            window.minSize = new Vector2(560, 320);
            window.Show();
        }

        private void OnEnable()
        {
            memberName = TeamActivitySettings.MemberName;
            deviceName = TeamActivitySettings.DeviceName;
            sharedDirectory = TeamActivitySettings.SharedDirectory;
            note = TeamActivityService.Note;
            noteSearchField = new SearchField();
            TeamActivityService.Changed += Repaint;
            EditorApplication.update += PublishPendingNote;
        }

        private void OnDisable()
        {
            TeamActivityService.Changed -= Repaint;
            EditorApplication.update -= PublishPendingNote;
            PublishNoteImmediately();
        }

        private void OnGUI()
        {
            var currentEvent = Event.current;
            var releaseNoteFocus = currentEvent.type == EventType.MouseDown &&
                noteSearchField != null &&
                noteSearchField.HasFocus();
            var mousePosition = currentEvent.mousePosition;

            DrawToolbar();
            DrawConflicts();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawMembers();
            EditorGUILayout.EndScrollView();

            DrawWorkControls();
            DrawSettings();

            if (releaseNoteFocus && !noteFieldRect.Contains(mousePosition))
            {
                PublishNoteImmediately();
                if (noteSearchField != null && noteSearchField.HasFocus())
                {
                    GUI.FocusControl(null);
                    EditorGUIUtility.editingTextField = false;
                }
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Team Activity - " + TeamActivityProjectIdentity.ProjectName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                TeamActivityService.PublishNow(false);
            }

            showSettings = GUILayout.Toggle(showSettings, "Settings", EditorStyles.toolbarButton, GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawConflicts()
        {
            var conflicts = TeamActivityService.GetConflicts();
            foreach (var conflict in conflicts)
            {
                EditorGUILayout.HelpBox(
                    "Conflict Risk (soft lock): " + ShortPath(conflict.assetPath) + "\n" +
                    string.Join(", ", conflict.memberNames.ToArray()),
                    MessageType.Warning);
            }
        }

        private static void DrawMembers()
        {
            var members = TeamActivityService.Members
                .OrderBy(member => member.GetStatus(DateTime.UtcNow, TeamActivitySettings.IdleSeconds, TeamActivitySettings.OfflineSeconds))
                .ThenBy(member => member.GetDisplayName())
                .ToList();

            if (members.Count == 0)
            {
                EditorGUILayout.HelpBox("No member presence has been found yet.", MessageType.Info);
                return;
            }

            foreach (var member in members)
            {
                DrawMember(member);
                EditorGUILayout.Space(4);
            }
        }

        private static void DrawMember(TeamMemberPresence member)
        {
            var status = member.GetStatus(DateTime.UtcNow, TeamActivitySettings.IdleSeconds, TeamActivitySettings.OfflineSeconds);
            var previousColor = GUI.color;
            GUI.color = StatusColor(status);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = previousColor;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(StatusIcon(status) + "  " + member.GetDisplayName(), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(status.ToString(), EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();

            DrawPathRow("Scene", member.scenePath);
            DrawPathRow("Working on", member.workingAssetPath);
            if (!string.IsNullOrWhiteSpace(member.note))
            {
                EditorGUILayout.LabelField("Note", member.note);
            }

            if (member.gitChangedFiles != null && member.gitChangedFiles.Count > 0)
            {
                EditorGUILayout.LabelField("Git changes", member.gitChangedFiles.Count.ToString());
                foreach (var path in member.gitChangedFiles.Take(8))
                {
                    EditorGUILayout.LabelField("   " + path, EditorStyles.miniLabel);
                }

                if (member.gitChangedFiles.Count > 8)
                {
                    EditorGUILayout.LabelField("   ... and " + (member.gitChangedFiles.Count - 8) + " more", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWorkControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("My work", EditorStyles.boldLabel);

            var selectedAsset = TeamActivityService.GetSelectedWorkAsset();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                "Claim",
                string.IsNullOrEmpty(TeamActivityService.PinnedAssetPath)
                    ? "Automatic: " + (string.IsNullOrEmpty(selectedAsset) ? "None" : ShortPath(selectedAsset))
                    : ShortPath(TeamActivityService.PinnedAssetPath));

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedAsset)))
            {
                if (GUILayout.Button("Pin selection", GUILayout.Width(100)))
                {
                    TeamActivityService.SetPinnedAsset(selectedAsset);
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(TeamActivityService.PinnedAssetPath)))
            {
                if (GUILayout.Button("Use automatic", GUILayout.Width(100)))
                {
                    TeamActivityService.SetPinnedAsset(string.Empty);
                }
            }
            EditorGUILayout.EndHorizontal();

            DrawNoteField();

            EditorGUILayout.EndVertical();
        }

        private void DrawNoteField()
        {
            if (noteSearchField == null)
            {
                noteSearchField = new SearchField();
            }

            var rowRect = EditorGUILayout.GetControlRect();
            noteFieldRect = EditorGUI.PrefixLabel(rowRect, new GUIContent("Note"));

            EditorGUI.BeginChangeCheck();
            var updatedNote = noteSearchField.OnGUI(noteFieldRect, note);
            if (EditorGUI.EndChangeCheck())
            {
                note = updatedNote;
                QueueNotePublish();
            }
        }

        private void QueueNotePublish()
        {
            notePublishPending = true;
            notePublishAt = EditorApplication.timeSinceStartup + NotePublishDelaySeconds;
        }

        private void PublishPendingNote()
        {
            if (!notePublishPending || EditorApplication.timeSinceStartup < notePublishAt)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Input.compositionString))
            {
                notePublishAt = EditorApplication.timeSinceStartup + NotePublishDelaySeconds;
                return;
            }

            PublishNoteImmediately();
        }

        private void PublishNoteImmediately()
        {
            if (!notePublishPending && string.Equals(TeamActivityService.Note, note, StringComparison.Ordinal))
            {
                return;
            }

            notePublishPending = false;
            TeamActivityService.SetNote(note);
        }

        private void DrawSettings()
        {
            if (!showSettings)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Settings (saved for this project and editor user)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Project", TeamActivityProjectIdentity.ProjectName);
            memberName = EditorGUILayout.TextField("Member name", memberName);
            deviceName = EditorGUILayout.TextField("Device name", deviceName);

            EditorGUILayout.BeginHorizontal();
            sharedDirectory = EditorGUILayout.TextField("Shared directory", sharedDirectory);
            if (GUILayout.Button("Choose...", GUILayout.Width(80)))
            {
                var chosen = EditorUtility.OpenFolderPanel("Team Activity shared directory", sharedDirectory, string.Empty);
                if (!string.IsNullOrEmpty(chosen))
                {
                    sharedDirectory = chosen;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "Project storage",
                Path.Combine(sharedDirectory, TeamActivityProjectIdentity.StorageFolderName),
                EditorStyles.miniLabel);

            var gitEnabled = EditorGUILayout.Toggle("Show Git changes", TeamActivitySettings.GitStatusEnabled);
            TeamActivitySettings.GitStatusEnabled = gitEnabled;
            EditorGUILayout.HelpBox(
                "For team sharing, every member should select the same network or cloud-synced folder. " +
                "Keep it outside Assets so presence files are not imported. " +
                "No project asset is locked; warnings are advisory only.",
                MessageType.Info);

            if (GUILayout.Button("Save settings and reconnect"))
            {
                SaveSettings();
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawPathRow(string label, string path)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, string.IsNullOrEmpty(path) ? "-" : ShortPath(path));
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                if (GUILayout.Button("Select", GUILayout.Width(55)))
                {
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "-";
            }

            var fileName = Path.GetFileName(path);
            return string.IsNullOrEmpty(fileName) ? path : fileName + "  (" + path + ")";
        }

        private static string StatusIcon(TeamMemberStatus status)
        {
            switch (status)
            {
                case TeamMemberStatus.Online: return "●";
                case TeamMemberStatus.Idle: return "●";
                default: return "○";
            }
        }

        private static Color StatusColor(TeamMemberStatus status)
        {
            switch (status)
            {
                case TeamMemberStatus.Online: return new Color(0.65f, 1f, 0.65f);
                case TeamMemberStatus.Idle: return new Color(1f, 0.9f, 0.55f);
                default: return new Color(0.75f, 0.75f, 0.75f);
            }
        }

        private void SaveSettings()
        {
            try
            {
                var normalizedDirectory = Path.GetFullPath(sharedDirectory);
                var assetsDirectory = Path.GetFullPath(Application.dataPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var candidate = normalizedDirectory
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                if (candidate.StartsWith(assetsDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    EditorUtility.DisplayDialog(
                        "Invalid Team Activity folder",
                        "Choose a folder outside this project's Assets directory.",
                        "OK");
                    return;
                }

                TeamActivitySettings.MemberName = memberName;
                TeamActivitySettings.DeviceName = deviceName;
                TeamActivitySettings.SharedDirectory = normalizedDirectory;
                TeamActivityService.RecreateTransport();
                TeamActivityService.PublishNow(true);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Invalid Team Activity folder", exception.Message, "OK");
            }
        }
    }
}
