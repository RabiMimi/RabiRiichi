using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Core;
using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Models {
    public class CreateRoomReq {
        [Required]
        public int sessionCode { get; set; }
    }

    public class CreateRoomResp {
        public int id { get; set; }

        public CreateRoomResp(int id) {
            this.id = id;
        }
    }

    public class Room {
        public int id;
        public Game game;
    }
}
