using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.JavaScript;

namespace MMXOnline
{
    // Browser implementation of the audio API described in Platform/Audio.cs, on top of audio.js (Web Audio).

    public class SoundBuffer : IDisposable
    {
        internal readonly int id;
        float duration = -1;

        public SoundBuffer(string path)
        {
            id = WebAudio.loadSound(File.ReadAllBytes(path));
        }

        // 0 while the browser is still decoding it
        public float Duration
        {
            get
            {
                if (duration < 0) duration = (float)WebAudio.soundDuration(id);
                return Math.Max(duration, 0);
            }
        }

        public void Dispose() { }
    }

    public class Sound : IDisposable
    {
        readonly int voice;
        // Whether it's playing and where is tracked here, so the game can check Status every frame without calling into JavaScript
        readonly Stopwatch playTime = new Stopwatch();
        float startOffset;
        float volume = 100;
        bool loop;

        public Sound(SoundBuffer soundBuffer)
        {
            SoundBuffer = soundBuffer;
            voice = WebAudio.createVoice();
        }

        public SoundBuffer SoundBuffer { get; }

        // Like SFML, playing always starts from the beginning
        public void Play() => start(0);

        void start(float offset)
        {
            startOffset = offset;
            if (WebAudio.playVoice(voice, SoundBuffer.id, offset, loop, volume / 100)) playTime.Restart();
            else playTime.Reset();
        }

        public void Stop()
        {
            WebAudio.stopVoice(voice);
            playTime.Reset();
            startOffset = 0;
        }

        public float Volume
        {
            get => volume;
            set
            {
                volume = value;
                WebAudio.setVoiceVolume(voice, value / 100);
            }
        }

        public bool Loop
        {
            get => loop;
            set
            {
                loop = value;
                WebAudio.setVoiceLoop(voice, value);
            }
        }

        public SoundStatus Status => playTime.IsRunning && (loop || elapsed < SoundBuffer.Duration) ? SoundStatus.Playing : SoundStatus.Stopped;

        float elapsed => startOffset + (float)playTime.Elapsed.TotalSeconds;

        public float PlayingOffset
        {
            get
            {
                if (Status != SoundStatus.Playing) return 0;
                float duration = SoundBuffer.Duration;
                return loop && duration > 0 ? elapsed % duration : elapsed;
            }
            // Like SFML, this has no effect on a stopped sound
            set
            {
                if (Status == SoundStatus.Playing) start(value);
            }
        }

        public void Dispose() => WebAudio.deleteVoice(voice);
    }

    public class Music : IDisposable
    {
        readonly string path;
        // Created the first time it plays, since most songs never do in a session
        int id;
        float volume = 100;
        bool loop;

        public Music(string path)
        {
            this.path = path;
        }

        int element
        {
            get
            {
                if (id == 0)
                {
                    id = WebAudio.createMusic(File.ReadAllBytes(path));
                    WebAudio.setMusicVolume(id, volume / 100);
                    WebAudio.setMusicLoop(id, loop);
                }
                return id;
            }
        }

        public void Play() => WebAudio.playMusic(element);

        public void Stop()
        {
            if (id != 0) WebAudio.stopMusic(id);
        }

        public float Volume
        {
            get => volume;
            set
            {
                volume = value;
                if (id != 0) WebAudio.setMusicVolume(id, value / 100);
            }
        }

        public bool Loop
        {
            get => loop;
            set
            {
                loop = value;
                if (id != 0) WebAudio.setMusicLoop(id, value);
            }
        }

        public float PlayingOffset
        {
            get => id != 0 ? (float)WebAudio.getMusicTime(id) : 0;
            set => WebAudio.setMusicTime(element, value);
        }

        public void Dispose() => Stop();
    }

    // Calls into audio.js (imported in main.js as the "platform" module's "audio" object)
    internal static partial class WebAudio
    {
        [JSImport("audio.loadSound", "platform")]
        internal static partial int loadSound([JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);

        [JSImport("audio.soundDuration", "platform")]
        internal static partial double soundDuration(int buffer);

        [JSImport("audio.createVoice", "platform")]
        internal static partial int createVoice();

        // Returns false if the sound hasn't finished decoding
        [JSImport("audio.playVoice", "platform")]
        internal static partial bool playVoice(int voice, int buffer, double offset, bool loop, double volume);

        [JSImport("audio.stopVoice", "platform")]
        internal static partial void stopVoice(int voice);

        [JSImport("audio.setVoiceVolume", "platform")]
        internal static partial void setVoiceVolume(int voice, double volume);

        [JSImport("audio.setVoiceLoop", "platform")]
        internal static partial void setVoiceLoop(int voice, bool loop);

        [JSImport("audio.deleteVoice", "platform")]
        internal static partial void deleteVoice(int voice);

        [JSImport("audio.createMusic", "platform")]
        internal static partial int createMusic([JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);

        [JSImport("audio.playMusic", "platform")]
        internal static partial void playMusic(int music);

        [JSImport("audio.stopMusic", "platform")]
        internal static partial void stopMusic(int music);

        [JSImport("audio.setMusicVolume", "platform")]
        internal static partial void setMusicVolume(int music, double volume);

        [JSImport("audio.setMusicLoop", "platform")]
        internal static partial void setMusicLoop(int music, bool loop);

        [JSImport("audio.getMusicTime", "platform")]
        internal static partial double getMusicTime(int music);

        [JSImport("audio.setMusicTime", "platform")]
        internal static partial void setMusicTime(int music, double seconds);
    }
}
