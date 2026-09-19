using Octokit;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SnowRunner_Tool
{
    class UpdateCheck
    {
        public static async Task<(int, string)> CheckGithubReleses(string assemblyVersion)
        {
            try
            {
                GitHubClient client = new GitHubClient(new ProductHeaderValue("SnowRunner-Tool"));
                var releases = await client.Repository.Release.GetAll("elpatron68", "SnowRunner-Tool");
                if (releases == null || releases.Count == 0)
                {
                    return (0, "https://github.com/elpatron68/SnowRunner-Tool/releases");
                }

                var latestRelease = releases[0];
                string downloadUrl = latestRelease.Assets?
                    .FirstOrDefault(a => a.Name != null &&
                        (a.Name.IndexOf("portable", StringComparison.OrdinalIgnoreCase) >= 0
                         || a.Name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0))?
                    .BrowserDownloadUrl
                    ?? latestRelease.HtmlUrl;

                string latestTag = (latestRelease.TagName ?? string.Empty).TrimStart('v', 'V');
                var thisVersion = new Version(assemblyVersion);
                var latestVersion = new Version(latestTag);
                int result = latestVersion.CompareTo(thisVersion);
                return (result, downloadUrl);
            }
            catch
            {
                return (0, "https://github.com/elpatron68/SnowRunner-Tool/releases");
            }
        }
    }
}
