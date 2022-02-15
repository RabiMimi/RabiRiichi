using System.IO;
using System.Reflection;

namespace RabiRiichi.Util {
    public static class Constants {
        public const string SERVICE_NAME = "rabiriichi";

        public static string BASE_DIR;

        static Constants() {
            BASE_DIR = Path.GetDirectoryName(
                new System.Uri(Assembly.GetExecutingAssembly().Location).LocalPath);
        }
    }
}
