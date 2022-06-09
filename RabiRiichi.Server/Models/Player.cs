namespace RabiRiichi.Server.Models {
    public enum PlayerStatus {
        None,
        Ready,
        Playing,
        Finished,
    }

    public class Player {
        public User user;
        public Room room;
        public PlayerStatus status = PlayerStatus.None;

        public Player(User user, Room room) {
            this.user = user;
            this.room = room;
        }
    }
}