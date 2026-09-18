using System;
using System.Collections.Generic;

namespace DevTools.TeamActivity
{
    internal enum TeamMemberStatus
    {
        Online,
        Idle,
        Offline
    }

    [Serializable]
    internal sealed class TeamMemberPresence
    {
        public int schemaVersion = 2;
        public string projectId;
        public string projectName;
        public string clientId;
        public string memberName;
        public string deviceName;
        public long lastHeartbeatUnixMs;
        public long lastActivityUnixMs;
        public string scenePath;
        public string workingAssetPath;
        public string note;
        public List<string> gitChangedFiles = new List<string>();

        public TeamMemberStatus GetStatus(DateTime utcNow, int idleSeconds, int offlineSeconds)
        {
            var now = new DateTimeOffset(utcNow).ToUnixTimeMilliseconds();
            if (now - lastHeartbeatUnixMs > offlineSeconds * 1000L)
            {
                return TeamMemberStatus.Offline;
            }

            return now - lastActivityUnixMs > idleSeconds * 1000L
                ? TeamMemberStatus.Idle
                : TeamMemberStatus.Online;
        }

        public string GetDisplayName()
        {
            var name = string.IsNullOrWhiteSpace(memberName) ? "Unknown" : memberName;
            return string.IsNullOrWhiteSpace(deviceName) ? name : name + " (" + deviceName + ")";
        }
    }

    internal sealed class ConflictInfo
    {
        public string assetPath;
        public readonly List<string> memberNames = new List<string>();
    }
}
