// Gamepad API access for the game's Platform/Web backend (see WebWindowEvents.cs), which polls it every frame.
// Browsers only report a gamepad after one of its buttons is pressed.

export const buttonCount = 17;
export const axisCount = 4;

// Fills state with: connected (0/1), the value (0-1) of each button in the standard layout, then the axes (-1 to 1).
// Standard layout: 0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 LT, 7 RT, 8 Back, 9 Start, 10 LS, 11 RS, 12-15 d-pad up/down/left/right, 16 Home;
// axes: 0 left X, 1 left Y, 2 right X, 3 right Y.
export function poll(index, state) {
    const values = new Float64Array(1 + buttonCount + axisCount);
    const pad = navigator.getGamepads?.()[index];
    if (pad && pad.connected) {
        values[0] = 1;
        for (let i = 0; i < buttonCount; i++) values[1 + i] = pad.buttons[i]?.value ?? 0;
        for (let i = 0; i < axisCount; i++) values[1 + buttonCount + i] = pad.axes[i] ?? 0;
    }
    state.set(values);
}

export function name(index) {
    return navigator.getGamepads?.()[index]?.id ?? '';
}
