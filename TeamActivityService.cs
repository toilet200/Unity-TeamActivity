using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevTools.TeamActivity
{
    [InitializeOnLoad]
    internal static class TeamActivityService
    {
        private static readonly GitStatusProvider GitStatus = new GitStatusProvider();
        private static ITeamActivityTransport transport;
        private static TeamMemberPresence localPresence;
        private static IReadOnlyList<TeamMemberPresence> members = new List<TeamMemberPresence>();
        private static double nextHeartbeat;
        private static double nextGitRefresh;
        private static double nextSoftLockWarning;
        private static double nextTransportErrorWarning;
        private static string pinnedAssetPath = string.Empty;
        private static string note = string.Empty;

        public static event Action Changed;

        public static IReadOnlyList<TeamMemberPresence> Members { get { return members; } }
        public static TeamMemberPresence LocalPresence { get { return localPresence; } }
        public static string PinnedAssetPath { get { return pinnedAssetPath; } }
        public static string Note { get { return note; } }

        static TeamActivityService()
        {
            RecreateTransport();
            localPresence = new TeamMemberPresence
            {
                projectId = TeamActivityProjectIdentity.ProjectId,
                projectName = TeamActivityProjectIdentity.ProjectName,
                clientId = TeamActivitySettings.ClientId,
                memberName = TeamActivitySettings.MemberName,
                deviceName = TeamActivitySettings.DeviceName,
                lastActivityUnixMs = UtcNowMs()
            };

            EditorApplication.update += Update;
            EditorApplication.quitting += OnQuitting;
            Selection.selectionChanged += OnEditorActivity;
            EditorApplication.projectChanged += OnEditorActivity;
            EditorApplication.hierarchyChanged += OnEditorActivity;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            Undo.undoRedoPerformed += OnEditorActivity;
            PublishNow(true);
        }

        public static void RecreateTransport()
        {
            transport = new FileSystemTeamActivityTransport(TeamActivitySettings.ProjectStorageDirectory);
            nextHeartbeat = 0;
        }

        public static void SetPinnedAsset(string assetPath)
        {
            pinnedAssetPath = NormalizeAssetPath(assetPath);
            MarkActivityAndPublish();
        }

        public static void SetNote(string value)
        {
            note = value ?? string.Empty;
            MarkActivityAndPublish();
        }

        public static string GetSelectedWorkAsset()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (selected is GameObject selectedObject)
            {
                var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedObject);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    path = prefabPath;
                }
            }

            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".prefab" || extension == ".cs" ? NormalizeAssetPath(path) : string.Empty;
        }

        public static IReadOnlyList<ConflictInfo> GetConflicts()
        {
            var activeMembers = members
                .Where(member => member.GetStatus(DateTime.UtcNow, TeamActivitySettings.IdleSeconds, TeamActivitySettings.OfflineSeconds) != TeamMemberStatus.Offline)
                .ToList();
            var claims = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var member in activeMembers)
            {
                AddClaim(claims, member.scenePath, member.GetDisplayName());
                AddClaim(claims, member.workingAssetPath, member.GetDisplayName());
            }

            return claims
                .Where(pair => pair.Value.Count > 1)
                .Select(pair =>
                {
                    var conflict = new ConflictInfo { assetPath = pair.Key };
                    conflict.memberNames.AddRange(pair.Value.OrderBy(name => name));
                    return conflict;
                })
                .OrderBy(conflict => conflict.assetPath)
                .ToList();
        }

        public static void PublishNow(bool markAsActivity)
        {
            if (markAsActivity)
            {
                localPresence.lastActivityUnixMs = UtcNowMs();
            }

            localPresence.schemaVersion = 2;
            localPresence.projectId = TeamActivityProjectIdentity.ProjectId;
            localPresence.projectName = TeamActivityProjectIdentity.ProjectName;
            localPresence.clientId = TeamActivitySettings.ClientId;
            localPresence.memberName = TeamActivitySettings.MemberName;
            localPresence.deviceName = TeamActivitySettings.DeviceName;
            localPresence.lastHeartbeatUnixMs = UtcNowMs();
            localPresence.scenePath = NormalizeAssetPath(SceneManager.GetActiveScene().path);
            localPresence.workingAssetPath = string.IsNullOrEmpty(pinnedAssetPath)
                ? GetSelectedWorkAsset()
                : pinnedAssetPath;
            localPresence.note = note;
            localPresence.gitChangedFiles = GitStatus.ChangedFiles.ToList();

            try
            {
                transport.Publish(localPresence);
                members = transport.ReadAll()
                    .Where(member => string.Equals(
                        member.projectId,
                        TeamActivityProjectIdentity.ProjectId,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception exception)
            {
                if (EditorApplication.timeSinceStartup >= nextTransportErrorWarning)
                {
                    UnityEngine.Debug.LogWarning("Team Activity: shared directory is unavailable. " + exception.Message);
                    nextTransportErrorWarning = EditorApplication.timeSinceStartup + 60;
                }
                members = new[] { localPresence };
            }

            nextHeartbeat = EditorApplication.timeSinceStartup + TeamActivitySettings.HeartbeatSeconds;
            Changed?.Invoke();
        }

        private static void Update()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now >= nextGitRefresh)
            {
                GitStatus.Refresh();
                nextGitRefresh = now + TeamActivitySettings.GitRefreshSeconds;
            }

            if (now >= nextHeartbeat)
            {
                PublishNow(false);
            }
        }

        private static void OnEditorActivity()
        {
            MarkActivityAndPublish();
            WarnAboutSoftLock();
        }

        private static void OnActiveSceneChanged(Scene previous, Scene current)
        {
            MarkActivityAndPublish();
            WarnAboutSoftLock();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            MarkActivityAndPublish();
            WarnAboutSoftLock();
        }

        private static void MarkActivityAndPublish()
        {
            localPresence.lastActivityUnixMs = UtcNowMs();
            PublishNow(false);
        }

        private static void WarnAboutSoftLock()
        {
            if (EditorApplication.timeSinceStartup < nextSoftLockWarning)
            {
                return;
            }

            var scenePath = NormalizeAssetPath(SceneManager.GetActiveScene().path);
            var assetPath = string.IsNullOrEmpty(pinnedAssetPath) ? GetSelectedWorkAsset() : pinnedAssetPath;
            var conflict = GetConflicts().FirstOrDefault(item =>
                string.Equals(item.assetPath, scenePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.assetPath, assetPath, StringComparison.OrdinalIgnoreCase));

            if (conflict != null)
            {
                UnityEngine.Debug.LogWarning(
                    "Team Activity - Conflict Risk (soft lock only): " + conflict.assetPath +
                    " is also being used by " + string.Join(", ", conflict.memberNames.ToArray()) + ".");
                nextSoftLockWarning = EditorApplication.timeSinceStartup + 15;
            }
        }

        private static void OnQuitting()
        {
            // Keep the member entry visible as Offline instead of silently removing it.
            // The same client id is reused and overwritten on the next editor launch.
            try
            {
                localPresence.lastHeartbeatUnixMs = 0;
                transport.Publish(localPresence);
            }
            catch { }
        }

        private static void AddClaim(IDictionary<string, HashSet<string>> claims, string path, string memberName)
        {
            path = NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!claims.TryGetValue(path, out var names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                claims[path] = names;
            }

            names.Add(string.IsNullOrEmpty(memberName) ? "Unknown" : memberName);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static long UtcNowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
