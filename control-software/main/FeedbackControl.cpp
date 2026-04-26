#include "FeedbackControl.h"

// ── Constructor ───────────────────────────────────────────────────────────────

FeedbackControl::FeedbackControl(const uint8_t* ledPins, uint8_t buzzerPin)
    : _buzzerPin(buzzerPin), _buzzerActive(false)
{
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        _leds[i].pin          = ledPins[i];
        _leds[i].mode         = LedMode::OFF;
        _leds[i].periodStartMs = 0;
    }
}

void FeedbackControl::begin() {
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        pinMode(_leds[i].pin, OUTPUT);
        digitalWrite(_leds[i].pin, LOW);
    }
    pinMode(_buzzerPin, OUTPUT);
    digitalWrite(_buzzerPin, LOW);
}

// ── Public ────────────────────────────────────────────────────────────────────

void FeedbackControl::setMode(LedId id, LedMode mode) {
    uint8_t i = static_cast<uint8_t>(id);
    if (i >= LED_COUNT) return;

    LedState& led     = _leds[i];
    led.mode          = mode;
    led.periodStartMs = millis();

    switch (mode) {
        case LedMode::OFF:   digitalWrite(led.pin, LOW);  break;
        case LedMode::ON:    digitalWrite(led.pin, HIGH); break;
        default: break;  // BLINK and FLASH are handled in update()
    }
}

LedMode FeedbackControl::getMode(LedId id) const {
    uint8_t i = static_cast<uint8_t>(id);
    if (i >= LED_COUNT) return LedMode::OFF;
    return _leds[i].mode;
}

void FeedbackControl::setModeDisplay(uint8_t modeValue) {
    setMode(LedId::GREEN_1, (modeValue & 0x01) ? LedMode::ON : LedMode::OFF);
    setMode(LedId::GREEN_2, (modeValue & 0x02) ? LedMode::ON : LedMode::OFF);
}

void FeedbackControl::setBuzzerActive(bool active) {
    _buzzerActive = active;
    if (active) {
        tone(_buzzerPin, BUZZER_FREQ_HZ);
    } else {
        noTone(_buzzerPin);
        digitalWrite(_buzzerPin, LOW);
    }
}

bool FeedbackControl::isBuzzerActive() const {
    return _buzzerActive;
}

void FeedbackControl::update() {
    unsigned long now = millis();
    for (uint8_t i = 0; i < LED_COUNT; i++) {
        updateLed(_leds[i], now);
    }
}

// ── Private ───────────────────────────────────────────────────────────────────

void FeedbackControl::updateLed(LedState& led, unsigned long now) {
    uint16_t period;
    switch (led.mode) {
        case LedMode::BLINK: period = BLINK_PERIOD_MS; break;
        case LedMode::FLASH: period = FLASH_PERIOD_MS; break;
        default: return;  // OFF and ON are set immediately in setMode()
    }

    // Square wave: on for the first half-period, off for the second.
    uint16_t phase = (uint16_t)((now - led.periodStartMs) % period);
    digitalWrite(led.pin, (phase < (period / 2)) ? HIGH : LOW);
}
