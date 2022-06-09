using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Models {
    public class CreateUserReq {
        [Required]
        [StringLength(16, MinimumLength = 1)]
        public string nickname { get; set; }
    }

    public class CreateUserResp {
        public string sessionCode { get; set; }

        public CreateUserResp(long sessionCode) {
            this.sessionCode = sessionCode.ToString("x");
        }
    }

    public class UserInfoResp {
        public int? room { get; set; }

        public UserInfoResp(User user) {
            this.room = user.room?.id;
        }
    }

    public enum UserStatus {
        None,
        Ready,
        Playing,
        Finished,
    }

    public class User {
        public string nickname = "无名兔兔";
        public Room room;
        public UserStatus status = UserStatus.None;
        public long sessionCode = 0;
    }
}
