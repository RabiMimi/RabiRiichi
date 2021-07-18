namespace RabiRiichi.Riichi {
    public enum Wind {
        E, S, W, N
    }
    public class Player {
        public int id;
        public Wind wind;
        /// <summary> 立直棒数量，不可用于判定是否立直 </summary>
        public int riichiStick = 0;
        /// <summary> 手牌 </summary>
        public Hand hand = new Hand();
    }
}
