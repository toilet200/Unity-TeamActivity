using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DevTools.TeamActivity
{
    internal sealed class FileSystemTeamActivityTransport : ITeamActivityTransport
    {
        private readonly string directoryPath;

        public FileSystemTeamActivityTransport(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public void Publish(TeamMemberPresence presence)
        {
            Directory.CreateDirectory(directoryPath);
            var destination = GetPresencePath(presence.clientId);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(presence, true), new UTF8Encoding(false));
                File.Copy(temporary, destination, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        public IReadOnlyList<TeamMemberPresence> ReadAll()
        {
            var result = new List<TeamMemberPresence>();
            if (!Directory.Exists(directoryPath))
            {
                return result;
            }

            foreach (var path in Directory.GetFiles(directoryPath, "*.presence.json"))
            {
                try
                {
                    var presence = JsonUtility.FromJson<TeamMemberPresence>(File.ReadAllText(path));
                    if (presence != null && !string.IsNullOrEmpty(presence.clientId))
                    {
                        result.Add(presence);
                    }
                }
                catch (IOException)
                {
                    // A cloud sync client may expose a file while it is being replaced.
                }
                catch (UnauthorizedAccessException)
                {
                    // A temporarily unavailable share must not break the Unity editor.
                }
                catch (ArgumentException)
                {
                    // Ignore malformed data written by an older or interrupted client.
                }
            }

            return result;
        }

        public void Remove(string clientId)
        {
            var path = GetPresencePath(clientId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string GetPresencePath(string clientId)
        {
            return Path.Combine(directoryPath, clientId + ".presence.json");
        }
    }
}
