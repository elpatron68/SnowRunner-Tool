using Octokit;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowRunner_Tool
{
    class UpdateCheck
    {
        private readonly ILogger _log = Log.ForContext<UpdateCheck>();

        public async static Task<(int, string)> CheckGithubReleses(string assemblyVersion)
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue("SnowRunner-Tool"));
            var releases = await client.Repository.Release.GetAll("elpatron68", "SnowRunner-Tool");
            // (new System.Collections.Generic.Mscorlib_CollectionDebugView<Octokit.ReleaseAsset>((new System.Collections.Generic.Mscorlib_CollectionDebugView<Octokit.Release>(releases).Items[0]).Assets).Items[1]).BrowserDownloadUrl
            string downloadUrl = releases[0].Assets[1].BrowserDownloadUrl;
            string latest = releases[0].TagName;
            var thisVersion = new Version(assemblyVersion);
            var latestVersion = new Version(latest);
            int result = latestVersion.CompareTo(thisVersion);
            Log.Debug("Updatecheck returned {UpdateCheckResult}", result);
            return (result, downloadUrl);
        }
    }
}
