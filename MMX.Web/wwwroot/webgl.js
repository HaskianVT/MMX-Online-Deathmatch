// WebGL2 renderer used by the game's Platform/Web backend (see WebGraphics.cs).
// C# sends triangles already in clip space; this draws them with the right texture, shader, blend mode and target.

let gl;
let canvas;
let vertexShader;
let defaultProgram;
const textures = [null];  // id -> { texture, framebuffer, width, height }
const programs = [null];  // id -> { program, uniforms: Map name -> location, textureUniforms: Map location -> texture id }

const vertexSource = `#version 300 es
in vec2 a_position;
in vec2 a_texCoord;
in vec4 a_color;
out vec2 v_texCoord;
out vec4 v_color;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_texCoord = a_texCoord;
    v_color = a_color;
}`;

// Used when no shader is set, like SFML's fixed pipeline
const defaultFragmentSource = `#version 300 es
precision highp float;
in vec2 v_texCoord;
in vec4 v_color;
uniform sampler2D u_texture;
uniform bool u_hasTexture;
out vec4 fragColor;
void main() {
    fragColor = u_hasTexture ? texture(u_texture, v_texCoord) * v_color : v_color;
}`;

export function init(canvasElement) {
    canvas = canvasElement;
    gl = canvas.getContext('webgl2', { alpha: false, antialias: false, premultipliedAlpha: false });
    if (!gl) throw new Error('WebGL2 is not supported by this browser.');

    vertexShader = compile(gl.VERTEX_SHADER, vertexSource);
    defaultProgram = link(defaultFragmentSource);

    const buffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    // x, y, u, v, r, g, b, a
    const stride = 8 * 4;
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, stride, 0);
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 2, gl.FLOAT, false, stride, 2 * 4);
    gl.enableVertexAttribArray(2);
    gl.vertexAttribPointer(2, 4, gl.FLOAT, false, stride, 4 * 4);

    gl.enable(gl.BLEND);
    gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
}

function compile(type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(log);
    }
    return shader;
}

function link(fragmentSource) {
    const fragmentShader = compile(gl.FRAGMENT_SHADER, fragmentSource);
    const program = gl.createProgram();
    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.bindAttribLocation(program, 0, 'a_position');
    gl.bindAttribLocation(program, 1, 'a_texCoord');
    gl.bindAttribLocation(program, 2, 'a_color');
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        throw new Error(gl.getProgramInfoLog(program));
    }
    const entry = { program, uniforms: new Map(), textureUniforms: new Map() };
    gl.useProgram(program);
    // The current texture always goes in unit 0
    gl.uniform1i(location(entry, 'u_texture'), 0);
    return entry;
}

function location(entry, name) {
    if (!entry.uniforms.has(name)) entry.uniforms.set(name, gl.getUniformLocation(entry.program, name));
    return entry.uniforms.get(name);
}

export function maxTextureSize() {
    return gl.getParameter(gl.MAX_TEXTURE_SIZE);
}

export function canvasSize(width, height) {
    canvas.width = width;
    canvas.height = height;
}

function newTexture(width, height, pixels) {
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return texture;
}

export function createTexture(width, height, pixels) {
    // The memory view is only valid during this call; texImage2D copies it
    const texture = newTexture(width, height, pixels._unsafe_create_view());
    return textures.push({ texture, framebuffer: null, width, height }) - 1;
}

export function createRenderTexture(width, height) {
    const texture = newTexture(width, height, null);
    const framebuffer = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0);
    return textures.push({ texture, framebuffer, width, height }) - 1;
}

// TrueType text: drawn with the canvas 2D API in the game's font, then uploaded as a texture

const fontFaces = [null];  // id -> FontFace
let textContext;

function textCanvasContext() {
    textContext ??= new OffscreenCanvas(1, 1).getContext('2d');
    return textContext;
}

export function loadFont(bytes) {
    const id = fontFaces.length;
    const face = new FontFace('mmxfont' + id, bytes.slice().buffer);
    fontFaces.push(face);
    face.load().then(loaded => document.fonts.add(loaded), e => console.warn('Could not load font', e));
    return id;
}

// Until the font finishes loading the browser draws with a fallback font, so text drawn before that isn't cached
export function fontReady(fontId) {
    return fontFaces[fontId]?.status === 'loaded';
}

function cssFont(fontId, size, style) {
    return ((style & 2) ? 'italic ' : '') + ((style & 1) ? 'bold ' : '') + size + 'px mmxfont' + fontId;
}

export function measureText(fontId, text, size, style) {
    const context = textCanvasContext();
    context.font = cssFont(fontId, size, style);
    return context.measureText(text).width;
}

// Draws lines of text on a width x height canvas, the first baseline at firstBaseline, and returns it as a texture
export function renderText(fontId, lines, size, style, fillColor, outlineColor, outlineThickness, padding, firstBaseline, lineSpacing, width, height) {
    const canvas = new OffscreenCanvas(width, height);
    const context = canvas.getContext('2d');
    context.font = cssFont(fontId, size, style);
    context.textBaseline = 'alphabetic';
    context.lineJoin = 'round';
    const textLines = lines.split('\n');
    for (let i = 0; i < textLines.length; i++) {
        const y = firstBaseline + i * lineSpacing;
        if (outlineThickness > 0) {
            // Strokes are centered on the outline, so double the width to get the thickness outside the letters
            context.strokeStyle = outlineColor;
            context.lineWidth = outlineThickness * 2;
            context.strokeText(textLines[i], padding, y);
        }
        context.fillStyle = fillColor;
        context.fillText(textLines[i], padding, y);
    }

    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, canvas);
    // Smooth like SFML's font textures; the game draws text at 4x and scales it down
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return textures.push({ texture, framebuffer: null, width, height }) - 1;
}

export function deleteTexture(id) {
    const entry = textures[id];
    if (!entry) return;
    gl.deleteTexture(entry.texture);
    if (entry.framebuffer) gl.deleteFramebuffer(entry.framebuffer);
    textures[id] = null;
}

export function createShader(fragmentSource) {
    try {
        return programs.push(link(fragmentSource)) - 1;
    } catch (e) {
        console.error('Shader compile error:', e.message, '\n' + fragmentSource);
        return 0;
    }
}

export function setUniformFloat(id, name, x) {
    const entry = programs[id];
    gl.useProgram(entry.program);
    gl.uniform1f(location(entry, name), x);
}

export function setUniformInt(id, name, x) {
    const entry = programs[id];
    gl.useProgram(entry.program);
    gl.uniform1i(location(entry, name), x);
}

export function setUniformVec4(id, name, x, y, z, w) {
    const entry = programs[id];
    gl.useProgram(entry.program);
    gl.uniform4f(location(entry, name), x, y, z, w);
}

export function setUniformTexture(id, name, textureId) {
    const entry = programs[id];
    const loc = location(entry, name);
    if (loc) entry.textureUniforms.set(loc, textureId);
}

function bindTarget(target) {
    const entry = target ? textures[target] : null;
    gl.bindFramebuffer(gl.FRAMEBUFFER, entry ? entry.framebuffer : null);
    return entry ? [entry.width, entry.height] : [canvas.width, canvas.height];
}

export function clear(target, r, g, b, a) {
    const [width, height] = bindTarget(target);
    gl.viewport(0, 0, width, height);
    gl.clearColor(r, g, b, a);
    gl.clear(gl.COLOR_BUFFER_BIT);
}

const blendFactors = () => [gl.ZERO, gl.ONE, gl.SRC_COLOR, gl.ONE_MINUS_SRC_COLOR, gl.DST_COLOR, gl.ONE_MINUS_DST_COLOR,
    gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.DST_ALPHA, gl.ONE_MINUS_DST_ALPHA];
const blendEquations = () => [gl.FUNC_ADD, gl.FUNC_SUBTRACT, gl.FUNC_REVERSE_SUBTRACT];

export function draw(target, viewportX, viewportY, viewportW, viewportH, textureId, shaderId, blendMode, vertices, vertexCount) {
    bindTarget(target);
    gl.viewport(viewportX, viewportY, viewportW, viewportH);

    const entry = shaderId ? programs[shaderId] : defaultProgram;
    gl.useProgram(entry.program);

    const texture = textureId ? textures[textureId] : null;
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, texture ? texture.texture : null);
    if (!shaderId) gl.uniform1i(location(entry, 'u_hasTexture'), texture ? 1 : 0);

    let unit = 1;
    for (const [loc, id] of entry.textureUniforms) {
        gl.activeTexture(gl.TEXTURE0 + unit);
        gl.bindTexture(gl.TEXTURE_2D, textures[id]?.texture ?? null);
        gl.uniform1i(loc, unit);
        unit++;
    }

    const factors = blendFactors();
    const equations = blendEquations();
    gl.blendFuncSeparate(factors[blendMode & 15], factors[(blendMode >> 4) & 15], factors[(blendMode >> 12) & 15], factors[(blendMode >> 16) & 15]);
    gl.blendEquationSeparate(equations[(blendMode >> 8) & 15], equations[(blendMode >> 20) & 15]);

    gl.bufferData(gl.ARRAY_BUFFER, vertices._unsafe_create_view(), gl.STREAM_DRAW);
    gl.drawArrays(gl.TRIANGLES, 0, vertexCount);
}
