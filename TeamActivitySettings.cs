using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DevTools.TeamActivity
{
    internal static class TeamActivitySettings
    {
        private static string Prefix
        {
            get { return "DevTools.TeamActivity." + TeamActivityProjectIdentity.ProjectId + "."; }
        }

        public const int HeartbeatSeconds = 5;
        public const int IdleSeconds = 120;
        public const int OfflineSeconds = 30;
        public const int GitRefreshSeconds = 15;

        public static string MemberName
        {
            get { return GetString("MemberName", Environment.UserName); }
            set { EditorPrefs.SetString(Prefix + "MemberName", string.IsNullOrWhiteSpace(value) ? Environment.UserName : value.Trim()); }
        }

        public static string DeviceName
        {
            get { return GetString("DeviceName", Environment.MachineName); }
            set { EditorPrefs.SetString(Prefix + "DeviceName", string.IsNullOrWhiteSpace(value) ? Environment.MachineName : value.Trim()); }
        }

        public static string ClientId
        {
            get
            {
                var id = GetString("ClientId", string.Empty);
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    EditorPrefs.SetString(Prefix + "ClientId", id);
                }

                return id;
            }
        }

        public static string SharedDirectory
        {
            get
            {
                var defaultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TeamActivity"));
                return GetString("SharedDirectory", defaultPath);
            }
            set { EditorPrefs.SetString(Prefix + "SharedDirectory", value); }
        }

        public static bool GitStatusEnabled
        {
            get { return EditorPrefs.GetBool(Prefix + "GitStatusEnabled", true); }
            set { EditorPrefs.SetBool(Prefix + "GitStatusEnabled", value); }
        }

        public static string ProjectStorageDirectory
        {
            get { return Path.Combine(SharedDirectory, TeamActivityProjectIdentity.StorageFolderName); }
        }

        private static string GetString(string name, string defaultValue)
        {
            return EditorPrefs.GetString(Prefix + name, defaultValue);
        }
    }
}
