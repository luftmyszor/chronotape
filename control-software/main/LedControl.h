#pragma once
#include <Arduino.h>
#include "Config.h"

// ─────────────────────────────────────────────────────────────────────────────
// LedControl
//
// Drives LED_COUNT monochromatic LEDs connected to PWM-capable Arduino pins
// using analogWrite() (0–255).  Each LED is controlled independently.
//
// Animated modes (BREATHING, PULSE, FLASH) use a periodic waveform so no
// blocking delays are needed.  BREATHING and PULSE use a triangle wave;
// FLASH uses a square wave.  Period constants come from Config.h.
//
// update() must be called every loop iteration.
// ─────────────────────────────────────────────────────────────────────────────
class LedControl {
public:
    // pins: array of LED_COUNT PWM-capable pin numbers.
    explicit LedControl(const uint8_t* pins);

    void begin();

    // Switch one LED to the requested mode.
    // dimValue is only used in DIM mode (0–255, default DIM_DEFAULT ≈ 25 %).
    void setMode(LedId id, LedMode mode, uint8_t dimValue = DIM_DEFAULT);

    // Advance all animated LEDs; call every loop iteration.
    void update();

    LedMode getMode(LedId id) const;

private:
    struct LedState {
        uint8_t       pin;
        LedMode       mode;
        uint8_t       dimValue;
        unsigned long periodStartMs;  // Reference timestamp for cycling modes
    };

    LedState _leds[LED_COUNT];

    void updateLed(LedState& led, unsigned long now);
};
