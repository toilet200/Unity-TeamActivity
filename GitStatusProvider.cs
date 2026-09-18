using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DevTools.TeamActivity
{
    internal sealed class GitStatusProvider
    {
        private readonly string projectRoot;
        private readonly object sync = new object();
        private List<string> changedFiles = new List<string>();
        private volatile bool isRefreshing;

        public GitStatusProvider()
        {
            projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        public IReadOnlyList<string> ChangedFiles
        {
            get
            {
                lock (sync)
                {
                    return changedFiles.ToArray();
                }
            }
        }

        public void Refresh()
        {
            if (isRefreshing || !TeamActivitySettings.GitStatusEnabled)
            {
                return;
            }

            isRefreshing = true;
            Task.Run(() =>
            {
                var files = ReadGitStatus();
                lock (sync)
                {
                    changedFiles = files;
                }

                isRefreshing = false;
            });
        }

        private List<string> ReadGitStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "status --porcelain=v1 --untracked-files=normal",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new List<string>();
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(3000))
                    {
                        try { process.Kill(); } catch { }
                        return new List<string>();
                    }

                    return output
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(ParsePath)
                        .Where(path => !string.IsNullOrEmpty(path))
                        .Take(100)
                        .ToList();
                }
            }
            catch
            {
                // Git is optional. Missing executables and non-Git projects are valid.
                return new List<string>();
            }
        }

        private static string ParsePath(string porcelainLine)
        {
            if (porcelainLine.Length < 4)
            {
                return string.Empty;
            }

            var path = porcelainLine.Substring(3).Trim();
            var renameSeparator = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (renameSeparator >= 0)
            {
                path = path.Substring(renameSeparator + 4);
            }

            return path.Trim('"').Replace('\\', '/');
        }
    }
}
