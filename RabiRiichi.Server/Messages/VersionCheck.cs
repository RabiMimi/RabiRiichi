using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Messages {
    public class OutVersionCheck {
        public string game = ServerConstants.GAME;
        public string server = ServerConstants.SERVER;
        public string serverVersion => ServerConstants.SERVER_VERSION;
        public string gameVersion => RabiRiichi.VERSION;
        public string minClientVersion = ServerConstants.MIN_CLIENT_VERSION;
    }

    public class InVersionCheck {
        public string client;
        public string clientVersion;
        public string minServerVersion;
    }
}