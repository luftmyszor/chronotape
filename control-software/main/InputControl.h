#pragma once
#include <Arduino.h>
#include "Config.h"

// ─────────────────────────────────────────────────────────────────────────────
// InputControl
//
// Reads BTN_COUNT active-low push-buttons (INPUT_PULLUP).
// Provides per-button debouncing and distinguishes short presses from long
// presses.  Button combos (BTN_A held + BTN_B short press) are detected at
// the state-machine level in main.ino via isHeld().
//
// update() must be called every loop iteration.
//
// Usage:
//   ButtonEvent ev = input.getEvent(BtnId::B);  // consumes the event
//   bool held      = input.isHeld(BtnId::A);    // instantaneous query
//   input.suppressLongPress(BtnId::A);           // call after a combo to
//                                                // prevent a stray long press
// ─────────────────────────────────────────────────────────────────────────────
class InputControl {
public:
    // pins: array of BTN_COUNT button pin numbers (active-low, INPUT_PULLUP).
    explicit InputControl(const uint8_t* pins);

    void begin();

    // Sample all button states; call every loop iteration.
    void update();

    // Return (and clear) the most recent event for the given button.
    ButtonEvent getEvent(BtnId btn);

    // True while the given button is currently held (debounced press active).
    bool isHeld(BtnId btn) const;

    // Prevent a LONG_PRESS event from firing on the given button.
    // Call this after detecting a combo to avoid a stray long press.
    void suppressLongPress(BtnId btn);

private:
    struct BtnState {
        uint8_t       pin;
        bool          lastRaw;       // Raw read on the previous update()
        bool          debounced;     // Stable (debounced) level
        unsigned long lastChangeMs;  // millis() when lastRaw last changed
        unsigned long pressStartMs;  // millis() when a debounced press began
        bool          longFired;     // True once LONG_PRESS has fired this hold
        ButtonEvent   pendingEvent;  // Waiting to be consumed by the caller
    };

    BtnState _btns[BTN_COUNT];

    void processButton(BtnState& btn);
};
