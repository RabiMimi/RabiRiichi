using RabiRiichi.Server.Connections;
using System.Reflection;

namespace RabiRiichi.Server.Models {
    public class ServerInfo {
        public string game { get; set; } = ServerConstants.GAME;
        public string server { get; set; } = ServerConstants.SERVER;
        public string serverVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public string gameVersion => RabiRiichi.VERSION;
        public string minClientVersion { get; set; } = ServerConstants.MIN_CLIENT_VERSION;
    }
}