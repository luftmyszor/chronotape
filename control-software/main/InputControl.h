#pragma once
#include <Arduino.h>

// ── Tuning constants ──────────────────────────────────────────────────────────
constexpr uint16_t DEBOUNCE_MS   = 50;   // Stable-signal window
constexpr uint16_t LONG_PRESS_MS = 800;  // Hold duration for a long press

// ── ButtonEvent ───────────────────────────────────────────────────────────────
enum class ButtonEvent : uint8_t {
    NONE,
    SHORT_PRESS,   // Button pressed and released before LONG_PRESS_MS
    LONG_PRESS     // Button held for >= LONG_PRESS_MS (fires once per hold)
};

// ─────────────────────────────────────────────────────────────────────────────
// InputControl
//
// Reads two active-low push-buttons (MODE and ADJUST) using INPUT_PULLUP.
// Provides debouncing and distinguishes short presses from long presses.
//
// Typical wiring: one side of each button to the Arduino pin, other side to GND.
//
// update() must be called every loop iteration.
// After calling update(), retrieve events with getModeEvent() /
// getAdjustEvent(); each call consumes the pending event (returns NONE until
// the next qualifying press occurs).
// ─────────────────────────────────────────────────────────────────────────────
class InputControl {
public:
    InputControl(uint8_t modePin, uint8_t adjustPin);

    void begin();

    // Sample button states and update internal state machines.
    void update();

    // Return (and clear) the most recent event for each button.
    ButtonEvent getModeEvent();
    ButtonEvent getAdjustEvent();

private:
    struct ButtonState {
        uint8_t       pin;
        bool          lastRaw;       // Raw read on the previous update()
        bool          debounced;     // Stable (debounced) level
        unsigned long lastChangeMs;  // millis() when lastRaw last changed
        unsigned long pressStartMs;  // millis() when a debounced press began
        bool          longFired;     // True once a LONG_PRESS event has fired
        ButtonEvent   pendingEvent;  // Waiting to be consumed by caller
    };

    ButtonState _mode;
    ButtonState _adjust;

    // Process one button state machine; may set pendingEvent.
    void processButton(ButtonState& btn);
};
