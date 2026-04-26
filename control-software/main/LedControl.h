#pragma once
#include <Arduino.h>
#include "Config.h"

// ─────────────────────────────────────────────────────────────────────────────
// LedControl
//
// Drives LED_COUNT monochromatic LEDs connected to Arduino digital/PWM pins.
// Each LED is controlled independently via three modes:
//   OFF   – fully off
//   ON    – fully on
//   FLASH – rapid square-wave blink (period = FLASH_PERIOD_MS from Config.h)
//           used during alarm ringing
//
// update() must be called every loop iteration for FLASH to animate.
// ─────────────────────────────────────────────────────────────────────────────
class LedControl {
public:
    // pins: array of LED_COUNT pin numbers.
    explicit LedControl(const uint8_t* pins);

    void begin();

    // Switch one LED to the requested mode.
    void setMode(LedId id, LedMode mode);

    // Advance all animated (FLASH) LEDs; call every loop iteration.
    void update();

    LedMode getMode(LedId id) const;

private:
    struct LedState {
        uint8_t       pin;
        LedMode       mode;
        unsigned long periodStartMs;  // Reference timestamp for FLASH animation
    };

    LedState _leds[LED_COUNT];

    void updateLed(LedState& led, unsigned long now);
};
