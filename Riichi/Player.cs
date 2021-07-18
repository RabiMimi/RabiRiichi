namespace RabiRiichi.Riichi {
    public enum Wind {
        E, S, W, N
    }
    public class Player {
        public int id;
        public string nickname;
        public Game game;
        public Wind wind;
        /// <summary> 立直棒数量，不可用于判定是否立直 </summary>
        public int riichiStick = 0;
        /// <summary> 手牌 </summary>
        public Hand hand = new Hand();

        public Player(int id, Game game) {
            this.id = id;
            this.game = game;
            hand.game = game;
            hand.player = id;
        }

        public int NextPlayer => game.NextPlayer(id);
        public int PrevPlayer => game.PrevPlayer(id);
    }
}
