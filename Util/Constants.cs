using System.IO;
using System.Reflection;

namespace RabiRiichi.Util {
    public static class Constants {
        public const string SERVICE_NAME = "rabiriichi";

        private static string bASE_DIR;

        static Constants() {
            BASE_DIR = Path.GetDirectoryName(
                new System.Uri(Assembly.GetExecutingAssembly().Location).LocalPath);
        }

        public static string BASE_DIR { get => bASE_DIR; set => bASE_DIR = value; }
    }
}
