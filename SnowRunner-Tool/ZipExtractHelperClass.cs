using System.IO.Compression;
using System.IO;

namespace SnowRunner_Tool
{
    public class ZipExtractHelperClass
    {
        public static void ZipFileExtractToDirectory(string zipPath, string extractPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string completeFileName = Path.Combine(extractPath, entry.FullName);
                    string directory = Path.GetDirectoryName(completeFileName);

                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    entry.ExtractToFile(completeFileName, true);
                }
            }
        }
    }
}
