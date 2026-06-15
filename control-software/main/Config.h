// ─────────────────────────────────────────────────────────────────────────────
// Config.h  –  Central configuration for the Chronotape project.
//
// ALL hardware pins, stepper physics, button timings, LED animation speeds,
// and system timeouts are defined here as constexpr values.
// Enums for button events, LED modes, and button/LED identifiers are also
// defined here so every module can share the same vocabulary.
//
// Data is strictly volatile — no EEPROM is used anywhere.
//
// Hardware summary:
//   Buttons (4, active-low with software debounce):
//     BTN_MODE         – cycles the main system mode
//     BTN_INC          – increments the digit on the selected tape
//     BTN_NEXT_TAPE    – cycles the selected tape (1→2→3→4→1)
//     BTN_ALARM_TOGGLE – toggles alarm on/off; activates snooze when ringing
//
//   LEDs (4, monochromatic):
//     LED_GREEN_1 & LED_GREEN_2 – binary mode indicator (LSB, MSB)
//       00 = BASE_MODE, 01 = SETTING_MODE,
//       10 = ALARM_SETTING_MODE, 11 = TAPE_ADJUST_MODE
//     LED_BLUE – snooze indicator (BASE_MODE) / setting sub-mode indicator
//                  OFF = Time, BLINK = Date, ON = Year (SETTING_MODE)
//     LED_RED  – alarm enabled indicator
//
//   Buzzer:
//     BUZZER_PIN – active or passive buzzer driven by tone()/noTone()
// ─────────────────────────────────────────────────────────────────────────────
#pragma once
#include <Arduino.h>

// ── Hardware Pins ─────────────────────────────────────────────────────────────
constexpr uint8_t PIN_BTN_MODE         = 17;   // Cycles main modes    (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_INC          = 13;   // Increments digit     (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_NEXT_TAPE    = 15;   // Next tape            (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_ALARM_TOGGLE = 2;    // Alarm toggle / snooze (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_LIGHT        = 12;   // Alarm on/off / (active-low, INPUT_PULLUP)

constexpr uint8_t PIN_LED_GREEN_1 = 7;    // Mode indicator LSB
constexpr uint8_t PIN_LED_GREEN_2 = 2;    // Mode indicator MSB
constexpr uint8_t PIN_LED_BLUE    = 3;    // Snooze / sub-mode indicator
constexpr uint8_t PIN_LED_RED     = 5;    // Alarm enabled indicator
constexpr uint8_t PIN_LED_YELLOW  = 4;    // Projection light indicator

constexpr uint8_t PIN_BUZZER = 6;        // Alarm buzzer (active or passive)

// ── Button / LED array sizes ──────────────────────────────────────────────────
constexpr uint8_t BTN_COUNT = 4;
constexpr uint8_t LED_COUNT = 4;

// Pin arrays — static const so each translation unit gets its own copy
// (avoids ODR issues when the header is included in multiple .cpp files).
static const uint8_t BTN_PINS[BTN_COUNT] = {
    PIN_BTN_MODE, PIN_BTN_INC, PIN_BTN_NEXT_TAPE, PIN_BTN_ALARM_TOGGLE
};
static const uint8_t LED_PINS[LED_COUNT] = {
    PIN_LED_GREEN_1, PIN_LED_GREEN_2, PIN_LED_BLUE, PIN_LED_RED
};

// ── Stepper Motor Physics (TapeControl) ──────────────────────────────────────
constexpr uint8_t  TAPE_COUNT       = 4;      // HH:MM → 4 tape drives
constexpr uint8_t  TAPE_DIGITS      = 10;     // Digits 0–9 per tape
constexpr uint8_t  STEP_PHASES      = 4;      // Full-step: 4 phases
constexpr uint16_t STEPS_PER_DIGIT  = 1920;    // Physical steps per digit advance
constexpr uint16_t STEP_INTERVAL_MS = 2;      // Min. ms between consecutive steps

// ── Button Timing (InputControl) ─────────────────────────────────────────────
constexpr uint16_t DEBOUNCE_MS   = 50;    // Debounce settling time (ms)
constexpr uint16_t LONG_PRESS_MS = 800;   // Hold duration for a long press (ms)

// ── LED Animation Speeds (FeedbackControl) ────────────────────────────────────
constexpr uint16_t BLINK_PERIOD_MS = 500;   // Slow blink — Date sub-mode indicator
constexpr uint16_t FLASH_PERIOD_MS = 200;   // Rapid flash — alarm ringing indicator

// ── Buzzer (FeedbackControl) ──────────────────────────────────────────────────
constexpr uint16_t BUZZER_FREQ_HZ = 1000;  // Tone frequency for the alarm

// ── System Timeouts ───────────────────────────────────────────────────────────
constexpr uint32_t SET_MODE_TIMEOUT_MS = 10000UL;    // Auto-exit setup modes after 10 s
constexpr uint32_t SNOOZE_DURATION_MS  = 300000UL;   // Snooze duration: 5 minutes

// ── Button Identifiers ────────────────────────────────────────────────────────
enum class BtnId : uint8_t {
    MODE         = 0,   // Cycles main modes
    INC          = 1,   // Increments the digit on the selected tape
    NEXT_TAPE    = 2,   // Cycles selected tape
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
    BLUE    = 2,   // Snooze active (BASE_MODE) / sub-mode indicator (SETTING_MODE)
    RED     = 3    // Alarm enabled indicator
};

// ── LED Operating Modes ───────────────────────────────────────────────────────
enum class LedMode : uint8_t {
    OFF,    // Fully off
    ON,     // Fully on
    BLINK,  // Slow square-wave blink (BLINK_PERIOD_MS) — Date sub-mode indicator
    FLASH   // Rapid square-wave flash (FLASH_PERIOD_MS) — alarm ringing indicator
};
