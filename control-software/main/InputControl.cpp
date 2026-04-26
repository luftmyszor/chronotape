#include "InputControl.h"

// ── Constructor ───────────────────────────────────────────────────────────────

InputControl::InputControl(const uint8_t* pins) {
    for (uint8_t i = 0; i < BTN_COUNT; i++) {
        _btns[i] = { pins[i], false, false, 0, 0, false, ButtonEvent::NONE };
    }
}

void InputControl::begin() {
    for (uint8_t i = 0; i < BTN_COUNT; i++) {
        pinMode(_btns[i].pin, INPUT_PULLUP);
        _btns[i].lastRaw   = (digitalRead(_btns[i].pin) == LOW);
        _btns[i].debounced = _btns[i].lastRaw;
    }
}

// ── Public ────────────────────────────────────────────────────────────────────

void InputControl::update() {
    for (uint8_t i = 0; i < BTN_COUNT; i++) {
        processButton(_btns[i]);
    }
}

ButtonEvent InputControl::getEvent(BtnId btn) {
    uint8_t i = static_cast<uint8_t>(btn);
    if (i >= BTN_COUNT) return ButtonEvent::NONE;
    ButtonEvent e = _btns[i].pendingEvent;
    _btns[i].pendingEvent = ButtonEvent::NONE;
    return e;
}

bool InputControl::isHeld(BtnId btn) const {
    uint8_t i = static_cast<uint8_t>(btn);
    if (i >= BTN_COUNT) return false;
    return _btns[i].debounced;
}

void InputControl::suppressLongPress(BtnId btn) {
    uint8_t i = static_cast<uint8_t>(btn);
    if (i >= BTN_COUNT) return;
    _btns[i].longFired = true;
}

// ── Private ───────────────────────────────────────────────────────────────────

void InputControl::processButton(BtnState& btn) {
    bool raw = (digitalRead(btn.pin) == LOW);  // Active-low: pressed = LOW
    unsigned long now = millis();

    // Detect raw edge and restart the debounce window.
    if (raw != btn.lastRaw) {
        btn.lastRaw      = raw;
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

    // While the button is held, check for the long-press threshold.
    if (btn.debounced && !btn.longFired) {
        if (now - btn.pressStartMs >= LONG_PRESS_MS) {
            btn.pendingEvent = ButtonEvent::LONG_PRESS;
            btn.longFired    = true;
        }
    }
}
