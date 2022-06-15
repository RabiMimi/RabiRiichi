namespace RabiRiichi.Server.Messages {
    public static class OutMsgType {
        public const string Empty = "";
        public const string FinishInquiry = "fi";
        public const string GameEvent = "ev";
        public const string HeartBeat = "h";
        public const string Inquiry = "inq";
        public const string Other = "ot";
        public const string RoomState = "rm";
        public const string VersionCheck = "ver";
    }

    public static class InMsgType {
        public const string Empty = "";
        public const string HeartBeat = "h";
        public const string InquiryResponse = "inq";
        public const string RoomUpdate = "rmu";
        public const string VersionCheck = "ver";
    }
}