using RabiRiichi.Communication.Json;
using RabiRiichi.Riichi;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace RabiRiichiDev {
    class DevConsole {
        private static string PrettyPrintJson(string json) {
            var options = new JsonSerializerOptions() {
                WriteIndented = true
            };
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            return JsonSerializer.Serialize(element, options);
        }

        private readonly Mutex consoleWriterMutex = new();

        public void WriteLine(string message) {
            consoleWriterMutex.WaitOne();
            Console.WriteLine(message);
            consoleWriterMutex.ReleaseMutex();
        }

        public void Start(GameConfig config) {
            var actionCenter = new JsonStringActionCenter((id, msg) => {
                WriteLine($"{id} ({msg.Length}B) > {PrettyPrintJson(msg)}");
            });
            config.actionCenter = actionCenter;
            var game = new Game(config);
            bool isFinished = false;
            Task.Run(async () => {
                await game.Start();
                isFinished = true;
            });
            while (true) {
                if (isFinished) {
                    break;
                }
                string line = Console.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                var split = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split.Length != 4) {
                    WriteLine("SYS > Unknown command.");
                    continue;
                }
                try {
                    int inquiryId = int.Parse(split[0]);
                    int playerId = int.Parse(split[1]);
                    int actionIndex = int.Parse(split[2]);
                    string message = split[3];
                    actionCenter.OnMessage(inquiryId, playerId, actionIndex, message);
                } catch (Exception e) {
                    WriteLine($"SYS > {e.Message}");
                    continue;
                }
            }
            WriteLine("SYS > Game Finished.");
        }

        static void Main() {
            var config = new GameConfig {
                playerCount = 4,
                seed = 114514,
            };
            new DevConsole().Start(config);
        }
    }
}
