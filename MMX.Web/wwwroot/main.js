import { dotnet } from './_framework/dotnet.js';
import * as gl from './webgl.js';
import * as audio from './audio.js';
import * as net from './net.js';

const status = document.getElementById('status');
const canvas = document.getElementById('canvas');

function setStatus(text) {
    status.textContent = text;
    status.style.display = text ? '' : 'none';
}

try {
    setStatus('Loading runtime...');
    const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create();
    setModuleImports('platform', { gl, audio, net });
    gl.init(canvas);

    const exports = await getAssemblyExports(getConfig().mainAssemblyName);
    const game = exports.MMXOnline.WebMain;
    await runMain();

    setStatus('Downloading assets...');
    const response = await fetch('assets.zip');
    if (!response.ok) throw new Error('Could not download assets.zip: ' + response.status);
    game.loadAssets(new Uint8Array(await response.arrayBuffer()));

    // The servers to play online on, if the site publishes a region.txt (e.g. { "name": "MyServer", "ip": "wss://mmx.example.com" })
    const regions = await fetch('region.txt');
    if (regions.ok) game.writeFile('region.txt', await regions.text());

    setStatus('Starting...');
    // Let the status text show before the long synchronous load
    await new Promise(resolve => setTimeout(resolve, 0));
    if (!game.start()) throw new Error('The game could not start.');
    setStatus('');

    forwardInput(game);
    canvas.focus();

    const frame = () => {
        try {
            game.frame();
        } catch (e) {
            setStatus('Error: ' + e.message);
            console.error(e.stack || e);
            return;
        }
        requestAnimationFrame(frame);
    };
    requestAnimationFrame(frame);
} catch (e) {
    setStatus('Error: ' + e.message);
    console.error(e.stack || e);
}

function forwardInput(game) {
    // Keys the browser would otherwise use for scrolling or focus
    const blockedKeys = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space', 'Tab', 'Backspace', 'F1', 'F11', 'F12']);
    window.addEventListener('keydown', e => {
        if (blockedKeys.has(e.code)) e.preventDefault();
        game.onKey(e.code, true);
    });
    window.addEventListener('keyup', e => {
        if (blockedKeys.has(e.code)) e.preventDefault();
        game.onKey(e.code, false);
    });

    // Mouse positions in canvas pixels, which can differ from its size on the page
    const toCanvas = e => {
        const rect = canvas.getBoundingClientRect();
        return [
            Math.round((e.clientX - rect.left) * canvas.width / rect.width),
            Math.round((e.clientY - rect.top) * canvas.height / rect.height)
        ];
    };
    canvas.addEventListener('mousemove', e => game.onMouseMove(...toCanvas(e)));
    canvas.addEventListener('mousedown', e => game.onMouseButton(e.button, true, ...toCanvas(e)));
    window.addEventListener('mouseup', e => game.onMouseButton(e.button, false, ...toCanvas(e)));
    canvas.addEventListener('contextmenu', e => e.preventDefault());
    canvas.addEventListener('wheel', e => {
        e.preventDefault();
        game.onMouseWheel(Math.sign(e.deltaY));
    }, { passive: false });
}
