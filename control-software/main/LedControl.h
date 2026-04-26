#pragma once
#include <Arduino.h>

// ── LED operating modes ───────────────────────────────────────────────────────
enum class LedMode : uint8_t {
    OFF,       // LED fully off
    ON,        // LED fully on (max brightness)
    DIM,       // LED at a fixed reduced brightness (set via setMode)
    BREATHING  // LED pulses smoothly between off and full brightness
};

// ─────────────────────────────────────────────────────────────────────────────
// LedControl
//
// Drives a single monochromatic LED connected to a PWM-capable Arduino pin
// using analogWrite() (0–255).
//
// BREATHING uses a triangle-wave envelope to minimise RAM/flash usage vs.
// a full sine table while still producing a smooth pulsing effect.
//
// update() must be called every loop iteration (only does work in BREATHING
// mode; all other modes are set-and-forget via analogWrite).
// ─────────────────────────────────────────────────────────────────────────────
class LedControl {
public:
    explicit LedControl(uint8_t pin);

    void begin();

    // Switch to the requested mode.
    // dimValue is only used in DIM mode (0–255, default 64 ≈ 25 %).
    void setMode(LedMode mode, uint8_t dimValue = 64);

    // Update the breathing animation; no-op for other modes.
    // Call every loop iteration.
    void update();

    LedMode getMode() const { return _mode; }

private:
    uint8_t       _pin;
    LedMode       _mode;
    uint8_t       _dimValue;
    unsigned long _breathStartMs; // millis() when the current breath cycle began

    // Duration of one full breath in / breath out cycle (ms).
    static const uint16_t BREATH_PERIOD_MS = 4000;
};
