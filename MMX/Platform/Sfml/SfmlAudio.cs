using System;

namespace MMXOnline
{
    // SFML implementation of the audio API described in Platform/Audio.cs

    public class SoundBuffer : IDisposable
    {
        internal readonly SFML.Audio.SoundBuffer buffer;

        public SoundBuffer(string path)
        {
            buffer = new SFML.Audio.SoundBuffer(path);
        }

        public float Duration => buffer.Duration.AsSeconds();

        public void Dispose() => buffer.Dispose();
    }

    public class Sound : IDisposable
    {
        readonly SFML.Audio.Sound sound;

        public Sound(SoundBuffer soundBuffer)
        {
            SoundBuffer = soundBuffer;
            sound = new SFML.Audio.Sound(soundBuffer.buffer);
        }

        public SoundBuffer SoundBuffer { get; }

        public void Play() => sound.Play();
        public void Stop() => sound.Stop();

        public float Volume
        {
            get => sound.Volume;
            set => sound.Volume = value;
        }

        public bool Loop
        {
            get => sound.Loop;
            set => sound.Loop = value;
        }

        public SoundStatus Status => (SoundStatus)sound.Status;

        public float PlayingOffset
        {
            get => sound.PlayingOffset.AsSeconds();
            set => sound.PlayingOffset = SFML.System.Time.FromSeconds(value);
        }

        public void Dispose() => sound.Dispose();
    }

    public class Music : IDisposable
    {
        readonly SFML.Audio.Music music;

        public Music(string path)
        {
            music = new SFML.Audio.Music(path);
        }

        public void Play() => music.Play();
        public void Stop() => music.Stop();

        public float Volume
        {
            get => music.Volume;
            set => music.Volume = value;
        }

        public bool Loop
        {
            get => music.Loop;
            set => music.Loop = value;
        }

        public float PlayingOffset
        {
            get => music.PlayingOffset.AsSeconds();
            set => music.PlayingOffset = SFML.System.Time.FromSeconds(value);
        }

        public void Dispose() => music.Dispose();
    }
}
