using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Event;
using System;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario {
    public class ScenarioActionCenter : IActionCenter {
        public void OnEvent(int playerId, EventBase ev) {
            OnMessage(ev.game, playerId, ev);
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            // TODO: Allow configuring ways to handle inquiry
            Task.Run(async () => {
                await Task.Yield();
                inquiry.Finish();
            });
        }

        public void OnMessage(Game game, int playerId, IRabiMessage msg) {
            Console.WriteLine($"{playerId} < {game.json.Stringify(msg, playerId)}");
        }
    }
}