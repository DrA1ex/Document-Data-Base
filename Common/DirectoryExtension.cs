using System.IO;
using System.Linq;

namespace Common
{
    public static class DirectoryExtension
    {
        public static long Size(this DirectoryInfo dir)
        {
            return dir.GetFiles().Sum(fi => fi.Length) +
                   dir.GetDirectories().Sum(di => Size(di));
        }
    }
}
