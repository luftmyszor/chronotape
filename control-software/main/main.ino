// ─────────────────────────────────────────────────────────────────────────────
// Chronotape — main.ino
//
// State-machine orchestrator.  All subsystems run non-blockingly; the only job
// of loop() is to tick every subsystem and handle the resulting events.
//
// State map:
//
//   NORMAL            Display current time.  LEDs breathe.
//                     BTN_A long         → SET_TIME_HOURS
//                     BTN_B short        → toggle alarm enable / disable
//                     BTN_A held + BTN_B → SET_ALARM_MODE
//                     alarm fires        → ALARM_RINGING
//
//   SET_TIME_HOURS    BTN_B short → increment hours & update display
//                     BTN_A short → SET_TIME_MINUTES
//                     BTN_A long or timeout → save time → NORMAL
//
//   SET_TIME_MINUTES  BTN_B short → increment minutes & update display
//                     BTN_A short → save time → SET_DATE_DAY
//                     BTN_A long or timeout → save time → NORMAL
//
//   SET_DATE_DAY      BTN_B short → increment day & update display
//                     BTN_A short → SET_DATE_MONTH
//                     BTN_A long or timeout → save date → NORMAL
//
//   SET_DATE_MONTH    BTN_B short → increment month & update display
//                     BTN_A short → SET_DATE_YEAR
//                     BTN_A long or timeout → save date → NORMAL
//
//   SET_DATE_YEAR     BTN_B short → increment year & update display
//                     BTN_A short → save date → SYNC_MODE
//                     BTN_A long or timeout → save date → NORMAL
//
//   SYNC_MODE         Time tracking paused.  Motors jogged manually.
//                     BTN_B short        → nudge selected tape +1 raw step
//                     BTN_A short        → select next tape
//                     BTN_A held + BTN_B → setZeroPoint(), → NORMAL
//                     BTN_A long         → exit without calibrating → NORMAL
//
//   SET_ALARM_MODE    Tapes show current alarm time.  Alarm LED pulses.
//                     BTN_A short → increment alarm hour, update tapes
//                     BTN_B short → increment alarm minute, update tapes
//                     BTN_A long or timeout → save alarm → back to current
//                                             time on tapes → NORMAL
//
//   ALARM_RINGING     Both LEDs flash rapidly.
//                     Any button press → dismiss alarm → NORMAL
// ─────────────────────────────────────────────────────────────────────────────

#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_MCP23X17.h>

#include "Config.h"
#include "TapeControl.h"
#include "DateTimeControl.h"
#include "InputControl.h"
#include "LedControl.h"

// ── Application states ────────────────────────────────────────────────────────
enum class AppState : uint8_t {
    NORMAL,
    SET_TIME_HOURS,
    SET_TIME_MINUTES,
    SET_DATE_DAY,
    SET_DATE_MONTH,
    SET_DATE_YEAR,
    SYNC_MODE,
    SET_ALARM_MODE,
    ALARM_RINGING
};

// ── Subsystem objects ─────────────────────────────────────────────────────────
Adafruit_MCP23X17 mcp;
TapeControl       tapes(mcp);
DateTimeControl   clock;
InputControl      input(BTN_PINS);
LedControl        leds(LED_PINS);

// ── State variables ───────────────────────────────────────────────────────────
static AppState      state        = AppState::NORMAL;
static uint8_t       calibTape    = 0;
static unsigned long inactivityMs = 0;  // millis() of last button activity

// ─────────────────────────────────────────────────────────────────────────────
// Display helpers
// ─────────────────────────────────────────────────────────────────────────────

static void displayCurrentTime() {
    uint8_t h = clock.getHours();
    uint8_t m = clock.getMinutes();
    tapes.moveTo(0, h / 10);
    tapes.moveTo(1, h % 10);
    tapes.moveTo(2, m / 10);
    tapes.moveTo(3, m % 10);
}

static void displayAlarmTime() {
    uint8_t ah = clock.getAlarmHour();
    uint8_t am = clock.getAlarmMinute();
    tapes.moveTo(0, ah / 10);
    tapes.moveTo(1, ah % 10);
    tapes.moveTo(2, am / 10);
    tapes.moveTo(3, am % 10);
}

// Display day, month, or year value on the tape pair used for that field.
// Day / month → tapes 2-3 (right pair, like the MM in HH:MM).
// Year → all four tapes showing "20YY".
static void displayDateDay() {
    uint8_t d = clock.getDay();
    tapes.moveTo(0, 0); tapes.moveTo(1, 0);
    tapes.moveTo(2, d / 10); tapes.moveTo(3, d % 10);
}

static void displayDateMonth() {
    uint8_t mo = clock.getMonth();
    tapes.moveTo(0, 0); tapes.moveTo(1, 0);
    tapes.moveTo(2, mo / 10); tapes.moveTo(3, mo % 10);
}

static void displayDateYear() {
    uint16_t yr = clock.getYear();
    tapes.moveTo(0, 2);
    tapes.moveTo(1, 0);
    tapes.moveTo(2, (uint8_t)((yr % 100) / 10));
    tapes.moveTo(3, (uint8_t)((yr % 100) % 10));
}

// ─────────────────────────────────────────────────────────────────────────────
// Inactivity timer helpers
// ─────────────────────────────────────────────────────────────────────────────

static void touchInactivity() {
    inactivityMs = millis();
}

static bool inactivityExpired() {
    return (millis() - inactivityMs) >= SET_MODE_TIMEOUT_MS;
}

// ─────────────────────────────────────────────────────────────────────────────
// State transitions
// ─────────────────────────────────────────────────────────────────────────────

// Forward declaration needed by enterState().
static void enterState(AppState next);

static void enterState(AppState next) {
    state = next;
    touchInactivity();

    switch (next) {

        case AppState::NORMAL:
            leds.setMode(LedId::STATUS, LedMode::BREATHING);
            leds.setMode(LedId::ALARM,
                         clock.isAlarmEnabled() ? LedMode::DIM : LedMode::OFF);
            clock.resumeTracking();
            displayCurrentTime();
            Serial.println(F("→ NORMAL"));
            break;

        case AppState::SET_TIME_HOURS:
            leds.setMode(LedId::STATUS, LedMode::ON);
            // Tapes already show current time; no additional movement needed.
            Serial.println(F("→ SET_TIME_HOURS"));
            break;

        case AppState::SET_TIME_MINUTES:
            leds.setMode(LedId::STATUS, LedMode::DIM);
            Serial.println(F("→ SET_TIME_MINUTES"));
            break;

        case AppState::SET_DATE_DAY:
            leds.setMode(LedId::STATUS, LedMode::FLASH);
            displayDateDay();
            Serial.println(F("→ SET_DATE_DAY"));
            break;

        case AppState::SET_DATE_MONTH:
            leds.setMode(LedId::STATUS, LedMode::FLASH);
            displayDateMonth();
            Serial.println(F("→ SET_DATE_MONTH"));
            break;

        case AppState::SET_DATE_YEAR:
            leds.setMode(LedId::STATUS, LedMode::FLASH);
            displayDateYear();
            Serial.println(F("→ SET_DATE_YEAR"));
            break;

        case AppState::SYNC_MODE:
            calibTape = 0;
            clock.pauseTracking();
            leds.setMode(LedId::STATUS, LedMode::OFF);
            Serial.print(F("→ SYNC  tape="));
            Serial.println(calibTape);
            break;

        case AppState::SET_ALARM_MODE:
            leds.setMode(LedId::STATUS, LedMode::PULSE);
            displayAlarmTime();
            Serial.println(F("→ SET_ALARM_MODE"));
            break;

        case AppState::ALARM_RINGING:
            leds.setMode(LedId::STATUS, LedMode::FLASH);
            leds.setMode(LedId::ALARM,  LedMode::FLASH);
            Serial.println(F("→ ALARM_RINGING"));
            break;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// setup
// ─────────────────────────────────────────────────────────────────────────────

void setup() {
    Serial.begin(9600);

    if (!mcp.begin_I2C()) {
        Serial.println(F("Error: MCP23017 not found. Check wiring."));
        while (1);
    }

    tapes.begin();
    tapes.loadCalibration();
    clock.begin();   // Loads time / date / alarm from EEPROM
    input.begin();
    leds.begin();

    enterState(AppState::NORMAL);
}

// ─────────────────────────────────────────────────────────────────────────────
// loop
// ─────────────────────────────────────────────────────────────────────────────

void loop() {
    // ── Tick all subsystems ───────────────────────────────────────────────────
    tapes.update();
    clock.update();
    input.update();
    leds.update();

    // ── In NORMAL mode, propagate time changes to the tape display ────────────
    if (state == AppState::NORMAL) {
        bool hc = clock.consumeHoursChanged();
        bool mc = clock.consumeMinutesChanged();
        if (hc || mc) {
            displayCurrentTime();
        }
        if (clock.consumeAlarmFired()) {
            enterState(AppState::ALARM_RINGING);
            return;
        }
    }

    // ── Read button events ────────────────────────────────────────────────────
    ButtonEvent evA = input.getEvent(BtnId::A);
    ButtonEvent evB = input.getEvent(BtnId::B);

    // Detect combo: BTN_A held while BTN_B fires a short press.
    bool aHeld = input.isHeld(BtnId::A);
    bool combo  = (evB == ButtonEvent::SHORT_PRESS) && aHeld;
    if (combo) {
        evB = ButtonEvent::NONE;           // Consume BTN_B into the combo
        input.suppressLongPress(BtnId::A); // Prevent a stray long press on A
    }

    // ── State machine ─────────────────────────────────────────────────────────
    switch (state) {

        // ── NORMAL ────────────────────────────────────────────────────────────
        case AppState::NORMAL:
            if (combo) {
                enterState(AppState::SET_ALARM_MODE);
            } else if (evA == ButtonEvent::LONG_PRESS) {
                enterState(AppState::SET_TIME_HOURS);
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                clock.toggleAlarmEnabled();
                clock.saveAlarm();
                leds.setMode(LedId::ALARM,
                             clock.isAlarmEnabled() ? LedMode::DIM : LedMode::OFF);
            }
            break;

        // ── SET_TIME_HOURS ────────────────────────────────────────────────────
        case AppState::SET_TIME_HOURS:
            if (evA == ButtonEvent::LONG_PRESS || inactivityExpired()) {
                clock.saveTime();
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                enterState(AppState::SET_TIME_MINUTES);
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.incrementHours();
                displayCurrentTime();
            }
            break;

        // ── SET_TIME_MINUTES ──────────────────────────────────────────────────
        case AppState::SET_TIME_MINUTES:
            if (evA == ButtonEvent::LONG_PRESS || inactivityExpired()) {
                clock.saveTime();
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.saveTime();
                enterState(AppState::SET_DATE_DAY);
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.incrementMinutes();
                displayCurrentTime();
            }
            break;

        // ── SET_DATE_DAY ──────────────────────────────────────────────────────
        case AppState::SET_DATE_DAY:
            if (evA == ButtonEvent::LONG_PRESS || inactivityExpired()) {
                clock.saveDate();
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                enterState(AppState::SET_DATE_MONTH);
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.incrementDay();
                displayDateDay();
            }
            break;

        // ── SET_DATE_MONTH ────────────────────────────────────────────────────
        case AppState::SET_DATE_MONTH:
            if (evA == ButtonEvent::LONG_PRESS || inactivityExpired()) {
                clock.saveDate();
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                enterState(AppState::SET_DATE_YEAR);
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.incrementMonth();
                displayDateMonth();
            }
            break;

        // ── SET_DATE_YEAR ─────────────────────────────────────────────────────
        case AppState::SET_DATE_YEAR:
            if (evA == ButtonEvent::LONG_PRESS || inactivityExpired()) {
                clock.saveDate();
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.saveDate();
                enterState(AppState::SYNC_MODE);
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.incrementYear();
                displayDateYear();
            }
            break;

        // ── SYNC_MODE ─────────────────────────────────────────────────────────
        case AppState::SYNC_MODE:
            if (combo) {
                // Mark all current tape positions as digit 0 and return.
                tapes.setZeroPoint();
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::LONG_PRESS) {
                // Cancel: exit without marking a zero point.
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                calibTape = (calibTape + 1) % TAPE_COUNT;
                Serial.print(F("SYNC  tape="));
                Serial.println(calibTape);
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                tapes.nudge(calibTape, 1);
            }
            break;

        // ── SET_ALARM_MODE ────────────────────────────────────────────────────
        case AppState::SET_ALARM_MODE:
            if (evA == ButtonEvent::LONG_PRESS || inactivityExpired()) {
                // Save the new alarm time and restore the time display.
                clock.saveAlarm();
                displayCurrentTime();
                enterState(AppState::NORMAL);
            } else if (evA == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.incrementAlarmHour();
                displayAlarmTime();
            } else if (evB == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                clock.incrementAlarmMinute();
                displayAlarmTime();
            }
            break;

        // ── ALARM_RINGING ─────────────────────────────────────────────────────
        case AppState::ALARM_RINGING:
            if (evA != ButtonEvent::NONE || evB != ButtonEvent::NONE) {
                clock.dismissAlarm();
                enterState(AppState::NORMAL);
            }
            break;
    }
}