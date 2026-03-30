namespace WilliamsSynth
{
    /// <summary>
    /// Enumeration of all predefined sound sequences in GameSoundSequences.
    /// Use this in the Unity Inspector to select which sound should play for an event.
    /// </summary>
    public enum GameSoundSequenceId
    {
        None,
        
        // Priority $FF
        CoinInsert,
        FreeShip,

        // Priority $F0
        PlayerDeath,
        Start1Player,
        Start2Player,

        // Priority $E8
        TerrainBlow,
        SmartBomb,

        // Priority $E0
        AstronautCatch,
        AstronautLand,
        AstronautHit,

        // Priority $D8
        AstronautScream,

        // Priority $D0
        Appear,
        ProbeHit,
        SchitzHit,
        UFOHit,
        TieHit,
        LanderDestroyed,
        LanderPickup,

        // Priority $C8
        LanderSuck,

        // Priority $C0
        SwarmerHit,
        Laser,
        LanderGrab,
        LanderShoot,
        SchitzoShoot,
        UFOShoot,
        SwarmerShoot,
        Hyperspace
    }
}
