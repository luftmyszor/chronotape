// ─────────────────────────────────────────────────────────────────────────────
// Config.h  –  Central configuration for the Chronotape project.
//
// ALL hardware pins, stepper physics, button timings, LED animation speeds,
// system timeouts, and EEPROM addresses are defined here as constexpr values.
// Enums for button events, LED modes, and logical button actions are also
// defined here so every module can share the same vocabulary.
// ─────────────────────────────────────────────────────────────────────────────
#pragma once
#include <Arduino.h>

// ── Hardware Pins ─────────────────────────────────────────────────────────────
constexpr uint8_t PIN_BTN_A      = 2;    // MODE / CONFIRM   (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_BTN_B      = 3;    // ADJUST / INCREMENT (active-low, INPUT_PULLUP)
constexpr uint8_t PIN_LED_STATUS = 9;    // Status LED  – must be PWM-capable
constexpr uint8_t PIN_LED_ALARM  = 10;   // Alarm LED   – must be PWM-capable

// ── Button / LED array sizes ──────────────────────────────────────────────────
constexpr uint8_t BTN_COUNT = 2;
constexpr uint8_t LED_COUNT = 2;

// Pin arrays — static const so each translation unit gets its own copy
// (avoids ODR issues when the header is included in multiple .cpp files).
static const uint8_t BTN_PINS[BTN_COUNT] = { PIN_BTN_A, PIN_BTN_B };
static const uint8_t LED_PINS[LED_COUNT] = { PIN_LED_STATUS, PIN_LED_ALARM };

// ── Stepper Motor Physics (TapeControl) ──────────────────────────────────────
constexpr uint8_t  TAPE_COUNT       = 4;      // HH:MM → 4 tape drives
constexpr uint8_t  TAPE_DIGITS      = 10;     // Digits 0–9 per tape
constexpr uint8_t  STEP_PHASES      = 4;      // Full-step: 4 phases
constexpr uint16_t STEPS_PER_DIGIT  = 200;    // Physical steps per digit advance
constexpr uint16_t STEP_INTERVAL_MS = 2;      // Min. ms between consecutive steps

// ── Button Timing (InputControl) ─────────────────────────────────────────────
constexpr uint16_t DEBOUNCE_MS   = 50;    // Debounce settling time (ms)
constexpr uint16_t LONG_PRESS_MS = 800;   // Hold duration for a long press (ms)

// ── LED Animation Speeds (LedControl) ────────────────────────────────────────
constexpr uint16_t BREATH_PERIOD_MS = 4000;   // Full breath cycle in NORMAL mode
constexpr uint16_t PULSE_PERIOD_MS  = 2000;   // Slow pulse for SET_ALARM_MODE
constexpr uint16_t FLASH_PERIOD_MS  = 200;    // Rapid flash period for alarm ring
constexpr uint8_t  DIM_DEFAULT      = 64;     // Default dim brightness (0–255, ≈ 25 %)

// ── System Timeouts ───────────────────────────────────────────────────────────
constexpr uint32_t SET_MODE_TIMEOUT_MS = 10000UL;  // Auto-exit setup modes after 10 s

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
enum class BtnId : uint8_t { A = 0, B = 1 };

// ── Raw Button Events (produced by InputControl) ──────────────────────────────
enum class ButtonEvent : uint8_t {
    NONE,
    SHORT_PRESS,   // Pressed and released before LONG_PRESS_MS
    LONG_PRESS     // Held for >= LONG_PRESS_MS (fires once per hold)
};

// ── LED Identifiers ───────────────────────────────────────────────────────────
enum class LedId : uint8_t { STATUS = 0, ALARM = 1 };

// ── LED Operating Modes ───────────────────────────────────────────────────────
enum class LedMode : uint8_t {
    OFF,        // Fully off
    ON,         // Fully on (max brightness)
    DIM,        // Fixed reduced brightness (set via dimValue parameter)
    BREATHING,  // Slow triangle-wave pulse — NORMAL mode status
    PULSE,      // Slow triangle-wave pulse — SET_ALARM_MODE indicator
    FLASH       // Rapid square-wave on/off — ALARM_RINGING indicator
};

// ── Logical Button Actions ────────────────────────────────────────────────────
// Defined here for documentation; the state machine in main.ino maps raw
// ButtonEvents + isHeld() queries to these logical actions.
enum class ButtonAction : uint8_t {
    NONE,
    ACTION_TOGGLE_ALARM,      // BTN_B short in NORMAL      → toggle alarm on/off
    ACTION_ENTER_SET_TIME,    // BTN_A long  in NORMAL      → enter time-setting flow
    ACTION_ENTER_ALARM_SET,   // BTN_A held + BTN_B short in NORMAL
    ACTION_INCREMENT,         // BTN_B short in SET_TIME / SET_DATE → increment field
    ACTION_CONFIRM_FIELD,     // BTN_A short in SET_TIME / SET_DATE → advance to next field
    ACTION_CANCEL_MODE,       // BTN_A long  in any setup mode      → return to NORMAL
    ACTION_INCREMENT_HOUR,    // BTN_A short in SET_ALARM_MODE
    ACTION_INCREMENT_MINUTE,  // BTN_B short in SET_ALARM_MODE
    ACTION_SAVE_ALARM,        // BTN_A long  in SET_ALARM_MODE → save + exit
    ACTION_NUDGE,             // BTN_B short in SYNC_MODE → nudge current tape +1 step
    ACTION_NEXT_TAPE,         // BTN_A short in SYNC_MODE → advance to next tape
    ACTION_SET_ZERO,          // BTN_A held + BTN_B short in SYNC_MODE → calibrate
    ACTION_DISMISS_ALARM      // Any press in ALARM_RINGING_MODE → stop alarm
};
