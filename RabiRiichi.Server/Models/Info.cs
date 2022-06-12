using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Models {
    public class ServerInfo {
        public string game { get; set; } = ServerConstants.GAME;
        public string server { get; set; } = ServerConstants.SERVER;
        public string serverVersion => ServerConstants.SERVER_VERSION;
        public string gameVersion => RabiRiichi.VERSION;
        public string minClientVersion { get; set; } = ServerConstants.MIN_CLIENT_VERSION;
    }
}