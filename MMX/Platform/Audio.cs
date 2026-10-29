namespace MMXOnline
{
    // Audio API the game uses. Each backend (e.g. Platform/Sfml) implements these classes with the same public members:
    //   SoundBuffer(string path)  Duration (seconds)
    //   Sound(SoundBuffer)        Play() Stop() Dispose()  Volume (0-100)  Loop  Status  PlayingOffset (seconds)  SoundBuffer
    //   Music(string path)        Play() Stop() Dispose()  Volume (0-100)  Loop  PlayingOffset (seconds)
    public enum SoundStatus
    {
        Stopped,
        Paused,
        Playing
    }
}
