#include "LedControl.h"

// ── Constructor ───────────────────────────────────────────────────────────────

LedControl::LedControl(const uint8_t* pins) {
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        _leds[i].pin          = pins[i];
        _leds[i].mode         = LedMode::OFF;
        _leds[i].periodStartMs = 0;
    }
}

void LedControl::begin() {
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        pinMode(_leds[i].pin, OUTPUT);
        digitalWrite(_leds[i].pin, LOW);
    }
}

// ── Public ────────────────────────────────────────────────────────────────────

void LedControl::setMode(LedId id, LedMode mode) {
    uint8_t i = static_cast<uint8_t>(id);
    if (i >= LED_COUNT) return;

    LedState& led     = _leds[i];
    led.mode          = mode;
    led.periodStartMs = millis();

    switch (mode) {
        case LedMode::OFF:   digitalWrite(led.pin, LOW);  break;
        case LedMode::ON:    digitalWrite(led.pin, HIGH); break;
        default: break;  // FLASH is handled in update()
    }
}

void LedControl::update() {
    unsigned long now = millis();
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        updateLed(_leds[i], now);
    }
}

LedMode LedControl::getMode(LedId id) const {
    uint8_t i = static_cast<uint8_t>(id);
    if (i >= LED_COUNT) return LedMode::OFF;
    return _leds[i].mode;
}

// ── Private ───────────────────────────────────────────────────────────────────

void LedControl::updateLed(LedState& led, unsigned long now) {
    if (led.mode != LedMode::FLASH) return;

    // Square wave: on for the first half-period, off for the second.
    uint16_t phase = (uint16_t)((now - led.periodStartMs) % FLASH_PERIOD_MS);
    digitalWrite(led.pin, (phase < (FLASH_PERIOD_MS / 2)) ? HIGH : LOW);
}
