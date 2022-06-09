using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Models {
    public class CreateUserReq {
        [Required]
        [StringLength(16, MinimumLength = 1)]
        public string nickname { get; set; }
    }

    public class CreateUserResp {
        public int sessionCode { get; set; }

        public CreateUserResp(int sessionCode) {
            this.sessionCode = sessionCode;
        }
    }

    public class User {
        public string nickname = "无名兔兔";

        public int sessionCode = 0;
    }
}
