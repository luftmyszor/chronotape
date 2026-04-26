#pragma once
#include <Arduino.h>
#include "Config.h"

// ─────────────────────────────────────────────────────────────────────────────
// FeedbackControl
//
// Manages the 4 monochromatic LEDs and the Buzzer as a unified feedback system.
//
// LED modes per LED:
//   OFF   – fully off
//   ON    – fully on
//   BLINK – slow square-wave (BLINK_PERIOD_MS) — Date sub-mode indicator
//   FLASH – fast square-wave (FLASH_PERIOD_MS) — alarm ringing indicator
//
// Buzzer:
//   setBuzzerActive(true)  — starts a continuous tone via tone()
//   setBuzzerActive(false) — silences the buzzer via noTone()
//
// setModeDisplay(value) — encodes a 2-bit value onto the Green LED pair:
//   bit 0 → LED_GREEN_1, bit 1 → LED_GREEN_2
//
// update() must be called every loop iteration for animated modes to work.
// ─────────────────────────────────────────────────────────────────────────────
class FeedbackControl {
public:
    // ledPins: array of LED_COUNT pin numbers (in LedId order).
    // buzzerPin: pin for the active or passive buzzer.
    FeedbackControl(const uint8_t* ledPins, uint8_t buzzerPin);

    void begin();

    // Set the mode for a single LED.
    void setMode(LedId id, LedMode mode);

    LedMode getMode(LedId id) const;

    // Encode a 2-bit mode value onto the Green LED pair.
    // bit 0 → GREEN_1, bit 1 → GREEN_2  (both set to ON or OFF as needed).
    void setModeDisplay(uint8_t modeValue);

    // Start (active=true) or stop (active=false) the alarm buzzer tone.
    void setBuzzerActive(bool active);

    bool isBuzzerActive() const;

    // Advance all animated LEDs; call every loop iteration.
    void update();

private:
    struct LedState {
        uint8_t       pin;
        LedMode       mode;
        unsigned long periodStartMs;  // Reference timestamp for animation
    };

    LedState _leds[LED_COUNT];
    uint8_t  _buzzerPin;
    bool     _buzzerActive;

    void updateLed(LedState& led, unsigned long now);
};
