namespace RabiRiichi.Event {
    class AfterDrawTileEvent : EventChain {
        public DrawTileEvent Source => source as DrawTileEvent;
    }
}
