using System;

namespace MMXOnline
{
    // Browser implementation of the audio API described in Platform/Audio.cs.
    // Silent for now: the prototype doesn't play sounds or music yet.

    public class SoundBuffer : IDisposable
    {
        public SoundBuffer(string path)
        {
        }

        public float Duration => 0;

        public void Dispose() { }
    }

    public class Sound : IDisposable
    {
        public Sound(SoundBuffer soundBuffer)
        {
            SoundBuffer = soundBuffer;
        }

        public SoundBuffer SoundBuffer { get; }

        public void Play() { }
        public void Stop() { }
        public float Volume { get; set; } = 100;
        public bool Loop { get; set; }
        public SoundStatus Status => SoundStatus.Stopped;
        public float PlayingOffset { get; set; }

        public void Dispose() { }
    }

    public class Music : IDisposable
    {
        public Music(string path)
        {
        }

        public void Play() { }
        public void Stop() { }
        public float Volume { get; set; } = 100;
        public bool Loop { get; set; }
        public float PlayingOffset { get; set; }

        public void Dispose() { }
    }
}
