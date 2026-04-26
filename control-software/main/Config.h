// ─────────────────────────────────────────────────────────────────────────────
// Config.h  –  Central configuration for the Chronotape project.
//
// ALL hardware pins, stepper physics, button timings, LED animation speeds,
// system timeouts, and EEPROM addresses are defined here as constexpr values.
// Enums for button events, LED modes, and button/LED identifiers are also
// defined here so every module can share the same vocabulary.
//
// Hardware summary:
//   Buttons (4, active-low with software debounce):
//     BTN_MODE         – cycles the main system mode
//     BTN_INC          – increments the selected value in setup modes
//     BTN_NEXT_TAPE    – cycles the selected tape / setting field
//     BTN_ALARM_TOGGLE – toggles alarm on/off; activates snooze when ringing
//
//   LEDs (5, monochromatic):
//     LED_GREEN_1 & LED_GREEN_2 – binary mode indicator (LSB, MSB)
//       00 = BASE_MODE, 01 = SETTING_MODE,
//       10 = ALARM_SETTING_MODE, 11 = TAPE_ADJUST_MODE
//     LED_BLUE   – snooze active indicator
//     LED_RED    – alarm enabled indicator
//     LED_YELLOW – sub-mode indicator (Time / Alarm-Hour vs Date / Alarm-Minute)
// ─────────────────────────────────────────────────────────────────────────────
#pragma once
#include <Arduino.h>

// ── Hardware Pins ─────────────────────────────────────────────────────────────
constexpr uint8_t PIN_BTN_MODE         = 2;   // Cycles main modes    (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_INC          = 3;   // Increments value     (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_NEXT_TAPE    = 4;   // Next tape / field    (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_ALARM_TOGGLE = 5;   // Alarm toggle / snooze (active-low, INPUT_PULLUP)

constexpr uint8_t PIN_LED_GREEN_1 = 6;    // Mode indicator LSB
constexpr uint8_t PIN_LED_GREEN_2 = 7;    // Mode indicator MSB
constexpr uint8_t PIN_LED_BLUE    = 8;    // Snooze active indicator
constexpr uint8_t PIN_LED_RED     = 9;    // Alarm enabled indicator
constexpr uint8_t PIN_LED_YELLOW  = 10;   // Sub-mode indicator

// ── Button / LED array sizes ──────────────────────────────────────────────────
constexpr uint8_t BTN_COUNT = 4;
constexpr uint8_t LED_COUNT = 5;

// Pin arrays — static const so each translation unit gets its own copy
// (avoids ODR issues when the header is included in multiple .cpp files).
static const uint8_t BTN_PINS[BTN_COUNT] = {
    PIN_BTN_MODE, PIN_BTN_INC, PIN_BTN_NEXT_TAPE, PIN_BTN_ALARM_TOGGLE
};
static const uint8_t LED_PINS[LED_COUNT] = {
    PIN_LED_GREEN_1, PIN_LED_GREEN_2, PIN_LED_BLUE, PIN_LED_RED, PIN_LED_YELLOW
};

// ── Stepper Motor Physics (TapeControl) ──────────────────────────────────────
constexpr uint8_t  TAPE_COUNT       = 4;      // HH:MM → 4 tape drives
constexpr uint8_t  TAPE_DIGITS      = 10;     // Digits 0–9 per tape
constexpr uint8_t  STEP_PHASES      = 4;      // Full-step: 4 phases
constexpr uint16_t STEPS_PER_DIGIT  = 200;    // Physical steps per digit advance
constexpr uint16_t STEP_INTERVAL_MS = 2;      // Min. ms between consecutive steps

// ── Button Timing (InputControl) ─────────────────────────────────────────────
constexpr uint16_t DEBOUNCE_MS   = 50;    // Debounce settling time (ms)
constexpr uint16_t LONG_PRESS_MS = 800;   // Hold duration for a long press (ms)

// ── LED Animation Speed (LedControl) ─────────────────────────────────────────
constexpr uint16_t FLASH_PERIOD_MS = 200;   // Rapid flash period for alarm ringing

// ── System Timeouts ───────────────────────────────────────────────────────────
constexpr uint32_t SET_MODE_TIMEOUT_MS = 10000UL;    // Auto-exit setup modes after 10 s
constexpr uint32_t SNOOZE_DURATION_MS  = 300000UL;   // Snooze duration: 5 minutes

// ── EEPROM Address Map ────────────────────────────────────────────────────────
// Total bytes used: 8 + TAPE_COUNT = 12
constexpr uint8_t EEPROM_ALARM_HOUR    = 0;   // 1 byte  (0–23)
constexpr uint8_t EEPROM_ALARM_MIN     = 1;   // 1 byte  (0–59)
constexpr uint8_t EEPROM_ALARM_ENABLED = 2;   // 1 byte  (0 = off, 1 = on)
constexpr uint8_t EEPROM_TIME_HOURS    = 3;   // 1 byte  (0–23)
constexpr uint8_t EEPROM_TIME_MINUTES  = 4;   // 1 byte  (0–59)
constexpr uint8_t EEPROM_DATE_DAY      = 5;   // 1 byte  (1–31)
constexpr uint8_t EEPROM_DATE_MONTH    = 6;   // 1 byte  (1–12)
constexpr uint8_t EEPROM_DATE_YEAR     = 7;   // 1 byte  (years since 2000; 25 → 2025)
constexpr uint8_t EEPROM_TAPE_BASE     = 8;   // TAPE_COUNT bytes: currentDigit[0..N-1]

// ── Button Identifiers ────────────────────────────────────────────────────────
enum class BtnId : uint8_t {
    MODE         = 0,   // Cycles main modes
    INC          = 1,   // Increments the selected value
    NEXT_TAPE    = 2,   // Cycles selected tape / setting field
    ALARM_TOGGLE = 3    // Toggles alarm on/off; snoozes when ringing
};

// ── Raw Button Events (produced by InputControl) ──────────────────────────────
enum class ButtonEvent : uint8_t {
    NONE,
    SHORT_PRESS,   // Pressed and released before LONG_PRESS_MS
    LONG_PRESS     // Held for >= LONG_PRESS_MS (fires once per hold)
};

// ── LED Identifiers ───────────────────────────────────────────────────────────
enum class LedId : uint8_t {
    GREEN_1 = 0,   // Mode display LSB
    GREEN_2 = 1,   // Mode display MSB
    BLUE    = 2,   // Snooze active indicator
    RED     = 3,   // Alarm enabled indicator
    YELLOW  = 4    // Sub-mode indicator
};

// ── LED Operating Modes ───────────────────────────────────────────────────────
enum class LedMode : uint8_t {
    OFF,    // Fully off
    ON,     // Fully on
    FLASH   // Rapid square-wave on/off — alarm ringing indicator
};
