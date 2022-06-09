using RabiRiichi.Server.Core;
using System.Reflection;

namespace RabiRiichi.Server.Models {
    public class ServerInfo {
        public string game { get; set; } = Constants.GAME;
        public string server { get; set; } = Constants.SERVER;
        public string serverVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public string gameVersion => RabiRiichi.VERSION;
        public string minClientVersion { get; set; } = Constants.MIN_CLIENT_VERSION;
    }
}