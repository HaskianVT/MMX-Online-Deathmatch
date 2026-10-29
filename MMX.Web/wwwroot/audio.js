// Web Audio backend used by the game's Platform/Web audio classes (see WebAudio.cs).
// Sound effects are decoded up front and played through AudioBufferSourceNodes ("voices");
// music is streamed with <audio> elements, created when a song first plays.

const context = new AudioContext();
const buffers = [null];  // id -> AudioBuffer once decoded
const voices = [null];   // id -> { source, gain }
const musics = [null];   // id -> { element, wantsToPlay }

// Browsers only allow audio after the user interacts with the page
let unlocked = false;
function unlock() {
    if (unlocked) return;
    unlocked = true;
    context.resume();
    for (const music of musics) {
        if (music?.wantsToPlay) music.element.play().catch(() => { });
    }
}
for (const type of ['keydown', 'mousedown', 'touchstart']) window.addEventListener(type, unlock, { capture: true });

// Returns the buffer id right away; the buffer can be played once decoding finishes
export function loadSound(bytes) {
    const id = buffers.push(null) - 1;
    const data = bytes.slice();
    context.decodeAudioData(data.buffer).then(buffer => buffers[id] = buffer, e => console.warn('Could not decode sound', id, e));
    return id;
}

// In seconds, or -1 while the sound is still decoding
export function soundDuration(id) {
    return buffers[id] ? buffers[id].duration : -1;
}

export function createVoice() {
    return voices.push({ source: null, gain: null }) - 1;
}

// The nodes only exist while the voice plays, so finished sounds don't stay in the audio graph
function release(voice) {
    voice.source.disconnect();
    voice.gain.disconnect();
    voice.source = null;
    voice.gain = null;
}

export function playVoice(voiceId, bufferId, offset, loop, volume) {
    stopVoice(voiceId);
    const buffer = buffers[bufferId];
    if (!buffer) return false;
    const voice = voices[voiceId];
    const gain = context.createGain();
    gain.gain.value = volume;
    gain.connect(context.destination);
    const source = context.createBufferSource();
    source.buffer = buffer;
    source.loop = loop;
    source.connect(gain);
    source.onended = () => {
        if (voice.source === source) release(voice);
    };
    source.start(0, Math.min(offset, buffer.duration));
    voice.source = source;
    voice.gain = gain;
    return true;
}

export function stopVoice(voiceId) {
    const voice = voices[voiceId];
    if (voice?.source) {
        voice.source.stop();
        release(voice);
    }
}

export function setVoiceVolume(voiceId, volume) {
    const voice = voices[voiceId];
    if (voice.gain) voice.gain.gain.value = volume;
}

export function setVoiceLoop(voiceId, loop) {
    const voice = voices[voiceId];
    if (voice.source) voice.source.loop = loop;
}

export function deleteVoice(voiceId) {
    stopVoice(voiceId);
    voices[voiceId] = null;
}

export function createMusic(bytes) {
    const element = new Audio(URL.createObjectURL(new Blob([bytes.slice()])));
    return musics.push({ element, wantsToPlay: false }) - 1;
}

export function playMusic(id) {
    const music = musics[id];
    music.wantsToPlay = true;
    if (unlocked) music.element.play().catch(() => { });
}

export function stopMusic(id) {
    const music = musics[id];
    music.wantsToPlay = false;
    music.element.pause();
    music.element.currentTime = 0;
}

export function setMusicVolume(id, volume) {
    musics[id].element.volume = Math.max(0, Math.min(1, volume));
}

export function setMusicLoop(id, loop) {
    musics[id].element.loop = loop;
}

export function getMusicTime(id) {
    return musics[id].element.currentTime;
}

export function setMusicTime(id, seconds) {
    musics[id].element.currentTime = seconds;
}
