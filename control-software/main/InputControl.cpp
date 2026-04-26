#include "InputControl.h"

// ── Constructor ───────────────────────────────────────────────────────────────

InputControl::InputControl(uint8_t modePin, uint8_t adjustPin) {
    _mode   = { modePin,   false, false, 0, 0, false, ButtonEvent::NONE };
    _adjust = { adjustPin, false, false, 0, 0, false, ButtonEvent::NONE };
}

void InputControl::begin() {
    pinMode(_mode.pin,   INPUT_PULLUP);
    pinMode(_adjust.pin, INPUT_PULLUP);

    // Initialise raw state so the first iteration has no spurious edge.
    _mode.lastRaw   = (digitalRead(_mode.pin)   == LOW);
    _adjust.lastRaw = (digitalRead(_adjust.pin) == LOW);
    _mode.debounced   = _mode.lastRaw;
    _adjust.debounced = _adjust.lastRaw;
}

// ── Public ────────────────────────────────────────────────────────────────────

void InputControl::update() {
    processButton(_mode);
    processButton(_adjust);
}

ButtonEvent InputControl::getModeEvent() {
    ButtonEvent e = _mode.pendingEvent;
    _mode.pendingEvent = ButtonEvent::NONE;
    return e;
}

ButtonEvent InputControl::getAdjustEvent() {
    ButtonEvent e = _adjust.pendingEvent;
    _adjust.pendingEvent = ButtonEvent::NONE;
    return e;
}

// ── Private ───────────────────────────────────────────────────────────────────

void InputControl::processButton(ButtonState& btn) {
    bool raw = (digitalRead(btn.pin) == LOW); // Active-low: pressed = LOW
    unsigned long now = millis();

    // Detect raw edge and restart the debounce window.
    if (raw != btn.lastRaw) {
        btn.lastRaw    = raw;
        btn.lastChangeMs = now;
    }

    // Wait for the signal to be stable for DEBOUNCE_MS.
    if (now - btn.lastChangeMs < DEBOUNCE_MS) return;

    // Debounced state has changed.
    if (raw != btn.debounced) {
        btn.debounced = raw;

        if (btn.debounced) {
            // Leading edge: button pressed.
            btn.pressStartMs = now;
            btn.longFired    = false;
        } else {
            // Trailing edge: button released.
            if (!btn.longFired) {
                // Released before long-press threshold → short press.
                btn.pendingEvent = ButtonEvent::SHORT_PRESS;
            }
        }
    }

    // While the button is held, check for long-press threshold.
    if (btn.debounced && !btn.longFired) {
        if (now - btn.pressStartMs >= LONG_PRESS_MS) {
            btn.pendingEvent = ButtonEvent::LONG_PRESS;
            btn.longFired    = true;
        }
    }
}
