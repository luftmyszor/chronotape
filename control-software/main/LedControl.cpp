#include "LedControl.h"

// ── Constructor ───────────────────────────────────────────────────────────────

LedControl::LedControl(const uint8_t* pins) {
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        _leds[i].pin          = pins[i];
        _leds[i].mode         = LedMode::OFF;
        _leds[i].dimValue     = DIM_DEFAULT;
        _leds[i].periodStartMs = 0;
    }
}

void LedControl::begin() {
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        pinMode(_leds[i].pin, OUTPUT);
        analogWrite(_leds[i].pin, 0);
    }
}

// ── Public ────────────────────────────────────────────────────────────────────

void LedControl::setMode(LedId id, LedMode mode, uint8_t dimValue) {
    uint8_t i = static_cast<uint8_t>(id);
    if (i >= LED_COUNT) return;

    LedState& led     = _leds[i];
    led.mode          = mode;
    led.dimValue      = dimValue;
    led.periodStartMs = millis();

    switch (mode) {
        case LedMode::OFF: analogWrite(led.pin, 0);          break;
        case LedMode::ON:  analogWrite(led.pin, 255);         break;
        case LedMode::DIM: analogWrite(led.pin, led.dimValue); break;
        default: break;  // Animated modes update in update()
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
    uint16_t period;
    switch (led.mode) {
        case LedMode::BREATHING: period = BREATH_PERIOD_MS; break;
        case LedMode::PULSE:     period = PULSE_PERIOD_MS;  break;
        case LedMode::FLASH:     period = FLASH_PERIOD_MS;  break;
        default: return;  // Static modes are already set by setMode()
    }

    uint16_t phase = (uint16_t)((now - led.periodStartMs) % period);

    if (led.mode == LedMode::FLASH) {
        // Square wave: on for the first half-period, off for the second.
        analogWrite(led.pin, (phase < (period / 2)) ? 255 : 0);
        return;
    }

    // Triangle-wave brightness for BREATHING and PULSE.
    uint16_t halfPeriod = period / 2;
    uint8_t brightness;
    if (phase < halfPeriod) {
        // Ramp up: 0 → 255 over the first half-period.
        brightness = (uint8_t)(((uint32_t)phase * 255UL) / halfPeriod);
    } else {
        // Ramp down: 255 → 0 over the second half-period.
        uint16_t t = phase - halfPeriod;
        brightness = (uint8_t)(255UL - ((uint32_t)t * 255UL) / halfPeriod);
    }
    analogWrite(led.pin, brightness);
}
