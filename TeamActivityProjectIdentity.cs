using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DevTools.TeamActivity
{
    internal static class TeamActivityProjectIdentity
    {
        private static readonly string CachedProjectId = ResolveProjectId();
        private static readonly string CachedProjectName = ResolveProjectName();

        public static string ProjectId { get { return CachedProjectId; } }
        public static string ProjectName { get { return CachedProjectName; } }

        public static string StorageFolderName
        {
            get
            {
                var safeName = new string(ProjectName
                    .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
                    .ToArray());
                return safeName + "-" + ProjectId.Substring(0, Math.Min(8, ProjectId.Length));
            }
        }

        private static string ResolveProjectId()
        {
            var projectSettingsPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../ProjectSettings/ProjectSettings.asset"));

            try
            {
                foreach (var line in File.ReadLines(projectSettingsPath))
                {
                    const string marker = "productGUID:";
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith(marker, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var value = trimmed.Substring(marker.Length).Trim();
                    if (Guid.TryParseExact(value, "N", out _))
                    {
                        return value.ToLowerInvariant();
                    }
                }
            }
            catch
            {
                // Fall back to a deterministic identity for unusual project layouts.
            }

            var seed = PlayerSettings.companyName + "/" + ResolveProjectName();
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(seed)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static string ResolveProjectName()
        {
            if (!string.IsNullOrWhiteSpace(PlayerSettings.productName))
            {
                return PlayerSettings.productName.Trim();
            }

            return new DirectoryInfo(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))).Name;
        }
    }
}
