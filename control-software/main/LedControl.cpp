#include "LedControl.h"

LedControl::LedControl(uint8_t pin)
    : _pin(pin), _mode(LedMode::OFF), _dimValue(64), _breathStartMs(0)
{}

void LedControl::begin() {
    pinMode(_pin, OUTPUT);
    analogWrite(_pin, 0);
}

void LedControl::setMode(LedMode mode, uint8_t dimValue) {
    _mode     = mode;
    _dimValue = dimValue;

    switch (_mode) {
        case LedMode::OFF:
            analogWrite(_pin, 0);
            break;
        case LedMode::ON:
            analogWrite(_pin, 255);
            break;
        case LedMode::DIM:
            analogWrite(_pin, _dimValue);
            break;
        case LedMode::BREATHING:
            _breathStartMs = millis();
            break;
    }
}

void LedControl::update() {
    if (_mode != LedMode::BREATHING) return;

    unsigned long now   = millis();
    // Position within the current breath cycle (0 … BREATH_PERIOD_MS-1).
    uint16_t phase = (uint16_t)((now - _breathStartMs) % BREATH_PERIOD_MS);

    uint8_t brightness;
    uint16_t halfPeriod = BREATH_PERIOD_MS / 2;

    if (phase < halfPeriod) {
        // Ramp up: 0 → 255 over the first half-period.
        brightness = (uint8_t)(((uint32_t)phase * 255UL) / halfPeriod);
    } else {
        // Ramp down: 255 → 0 over the second half-period.
        uint16_t t = phase - halfPeriod;
        brightness = (uint8_t)(255UL - ((uint32_t)t * 255UL) / halfPeriod);
    }

    analogWrite(_pin, brightness);
}
