using System.Reflection;

namespace RabiRiichi.Server.Utils {
    public static class ServerConstants {
        public const string GAME = "RabiRiichi";
        public const string SERVER = "vanilla";
        public const string MIN_CLIENT_VERSION = "0.1.0";
        public static string SERVER_VERSION => Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static readonly TimeSpan RESPONSE_TIMEOUT = TimeSpan.FromSeconds(5);
    }

    public static class ServerUtils {

        public static bool IsClientVersionSupported(string clientVersion) {
            try {
                return new Version(clientVersion) >= new Version(ServerConstants.MIN_CLIENT_VERSION);
            } catch (Exception) {
                return false;
            }
        }

        public static bool IsServerVersionSupported(string minServerVersion) {
            try {
                return new Version(ServerConstants.SERVER_VERSION) <= new Version(minServerVersion);
            } catch (Exception) {
                return false;
            }
        }
    }
}