// WebSocket networking used by the game's Platform/Web backend (see WebNetwork.cs), talking to the relay's WebSocket gateway:
//   /tcp         matchmaking: one framed request and response per connection (4-byte big-endian length, then the bytes)
//   /udp?port=N  one WebSocket message per Lidgren datagram, to and from relay UDP port N

// Resolves with the response payload as base64, or null if the request fails or times out
export function matchmakingRequest(url, query, timeoutMs) {
    return new Promise(resolve => {
        let done = false;
        let ws;
        const finish = result => {
            if (done) return;
            done = true;
            clearTimeout(timer);
            try { ws.close(); } catch { }
            resolve(result);
        };
        const timer = setTimeout(() => finish(null), timeoutMs);

        try {
            ws = new WebSocket(url);
        } catch {
            finish(null);
            return;
        }
        ws.binaryType = 'arraybuffer';

        let received = new Uint8Array(0);
        ws.onopen = () => {
            const payload = new TextEncoder().encode(query);
            const frame = new Uint8Array(4 + payload.length);
            new DataView(frame.buffer).setUint32(0, payload.length);
            frame.set(payload, 4);
            ws.send(frame);
        };
        ws.onmessage = e => {
            const chunk = new Uint8Array(e.data);
            const combined = new Uint8Array(received.length + chunk.length);
            combined.set(received);
            combined.set(chunk, received.length);
            received = combined;
            if (received.length < 4) return;
            const length = new DataView(received.buffer).getUint32(0);
            if (received.length >= 4 + length) finish(toBase64(received.subarray(4, 4 + length)));
        };
        ws.onerror = () => finish(null);
        ws.onclose = () => finish(null);
    });
}

function toBase64(bytes) {
    let binary = '';
    for (let i = 0; i < bytes.length; i += 0x8000) {
        binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
    }
    return btoa(binary);
}

const sockets = [null];  // id -> { ws, received: Uint8Array[], pending: Uint8Array[] }

// Opens a datagram WebSocket. Datagrams sent before it connects are sent once it does.
export function udpOpen(url) {
    const socket = { ws: new WebSocket(url), received: [], pending: [] };
    socket.ws.binaryType = 'arraybuffer';
    socket.ws.onopen = () => {
        for (const datagram of socket.pending) socket.ws.send(datagram);
        socket.pending = [];
    };
    socket.ws.onmessage = e => socket.received.push(new Uint8Array(e.data));
    return sockets.push(socket) - 1;
}

export function udpSend(id, bytes) {
    const socket = sockets[id];
    if (!socket) return;
    const datagram = bytes.slice();
    if (socket.ws.readyState === WebSocket.OPEN) socket.ws.send(datagram);
    else if (socket.ws.readyState === WebSocket.CONNECTING) socket.pending.push(datagram);
}

// Copies the next received datagram into buffer and returns its length, or 0 if there are none
export function udpReceive(id, buffer) {
    const socket = sockets[id];
    if (!socket || socket.received.length === 0) return 0;
    const datagram = socket.received.shift();
    if (datagram.length > buffer.length) return 0;
    buffer.set(datagram);
    return datagram.length;
}

export function udpClose(id) {
    const socket = sockets[id];
    if (!socket) return;
    try { socket.ws.close(); } catch { }
    sockets[id] = null;
}
