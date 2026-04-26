// ─────────────────────────────────────────────────────────────────────────────
// Chronotape — main.ino
//
// State-machine orchestrator.  All subsystems run non-blockingly; the only job
// of loop() is to tick every subsystem and handle the resulting events.
//
// Mode map (Green LED pair shows binary mode value):
//
//   BASE_MODE (00)
//     Tapes display current time.
//     BTN_MODE             → SETTING_MODE
//     BTN_ALARM_TOGGLE     → toggle alarm on/off (Red LED reflects state)
//     alarm fires          → alarm ringing (Green+Red LEDs flash, Buzzer on)
//       BTN_ALARM_TOGGLE   → snooze for SNOOZE_DURATION_MS (Blue LED on)
//       BTN_MODE / BTN_INC / BTN_NEXT_TAPE → dismiss alarm permanently
//
//   SETTING_MODE (01)
//     Sub-modes: TIME → DATE → YEAR, cycled when BTN_NEXT_TAPE wraps Tape4→Tape1.
//     Blue LED: OFF = Time, BLINK = Date, ON = Year
//     Tapes show the four digits of the current sub-mode value.
//     BTN_NEXT_TAPE → advance selected tape (0→1→2→3→0); wrapping advances sub-mode.
//     BTN_INC       → increment the digit on the selected tape (0–9).
//     BTN_MODE      → commit edits → ALARM_SETTING_MODE
//     Inactivity    → commit edits → BASE_MODE
//
//   ALARM_SETTING_MODE (10)
//     Tapes show the alarm time (four digits: AH/10, AH%10, AM/10, AM%10).
//     BTN_NEXT_TAPE → cycle selected tape 0→1→2→3→0.
//     BTN_INC       → increment the digit on the selected tape (0–9).
//     BTN_MODE      → commit alarm time → TAPE_ADJUST_MODE
//     Inactivity    → commit alarm time → BASE_MODE
//
//   TAPE_ADJUST_MODE (11)
//     Time tracking paused.  Tapes stay at current physical position.
//     BTN_NEXT_TAPE → select next tape/motor (cycles 0–3).
//     BTN_INC       → nudge selected motor +1 raw step for fine alignment.
//     BTN_MODE      → setZeroPoint() on all tapes → BASE_MODE
// ─────────────────────────────────────────────────────────────────────────────

#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_MCP23X17.h>

#include "Config.h"
#include "TapeControl.h"
#include "DateTimeControl.h"
#include "InputControl.h"
#include "FeedbackControl.h"

// ── Application modes ─────────────────────────────────────────────────────────
enum class AppMode : uint8_t {
    BASE_MODE          = 0,   // 00 – display time
    SETTING_MODE       = 1,   // 01 – set time / date / year
    ALARM_SETTING_MODE = 2,   // 10 – set alarm time
    TAPE_ADJUST_MODE   = 3    // 11 – manual tape calibration
};

// ── Setting sub-modes (used in SETTING_MODE) ──────────────────────────────────
enum class SettingSubMode : uint8_t { TIME_SUB, DATE_SUB, YEAR_SUB };

// ── Subsystem objects ─────────────────────────────────────────────────────────
Adafruit_MCP23X17  mcp;
TapeControl        tapes(mcp);
DateTimeControl    dt;
InputControl       input(BTN_PINS);
FeedbackControl    feedback(LED_PINS, PIN_BUZZER);

// ── State variables ───────────────────────────────────────────────────────────
static AppMode        appMode        = AppMode::BASE_MODE;
static bool           alarmRinging   = false;
static bool           snoozeActive   = false;
static unsigned long  snoozeUntilMs  = 0;
static unsigned long  inactivityMs   = 0;

// SETTING_MODE state
static SettingSubMode settingSubMode = SettingSubMode::TIME_SUB;
static uint8_t        selectedTape   = 0;
static uint8_t        editDigits[TAPE_COUNT];

// ALARM_SETTING_MODE state
static uint8_t        alarmSelectedTape = 0;
static uint8_t        alarmEditDigits[TAPE_COUNT];

// TAPE_ADJUST_MODE state
static uint8_t        calibTape = 0;

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
// SETTING_MODE helpers
// ─────────────────────────────────────────────────────────────────────────────

// Load editDigits from the current clock value for the active sub-mode.
static void loadEditDigitsFromClock() {
    switch (settingSubMode) {
        case SettingSubMode::TIME_SUB: {
            uint8_t h = dt.getHours(), m = dt.getMinutes();
            editDigits[0] = h / 10; editDigits[1] = h % 10;
            editDigits[2] = m / 10; editDigits[3] = m % 10;
            break;
        }
        case SettingSubMode::DATE_SUB: {
            uint8_t d = dt.getDay(), mo = dt.getMonth();
            editDigits[0] = d  / 10; editDigits[1] = d  % 10;
            editDigits[2] = mo / 10; editDigits[3] = mo % 10;
            break;
        }
        case SettingSubMode::YEAR_SUB: {
            uint16_t yr = dt.getYear();
            editDigits[0] = (yr / 1000) % 10;
            editDigits[1] = (yr / 100)  % 10;
            editDigits[2] = (yr / 10)   % 10;
            editDigits[3] =  yr         % 10;
            break;
        }
    }
}

// Commit editDigits to the clock for the current sub-mode.
static void commitEditDigits() {
    switch (settingSubMode) {
        case SettingSubMode::TIME_SUB: {
            uint8_t h = editDigits[0] * 10 + editDigits[1];
            uint8_t m = editDigits[2] * 10 + editDigits[3];
            if (h > 23) h = 23;
            if (m > 59) m = 59;
            dt.setTime(h, m);
            break;
        }
        case SettingSubMode::DATE_SUB: {
            uint8_t d  = editDigits[0] * 10 + editDigits[1];
            uint8_t mo = editDigits[2] * 10 + editDigits[3];
            if (mo < 1 || mo > 12) mo = 1;
            if (d  < 1 || d  > 31) d  = 1;
            dt.setDate(d, mo, dt.getYear());
            break;
        }
        case SettingSubMode::YEAR_SUB: {
            uint16_t yr = (uint16_t)editDigits[0] * 1000
                        + (uint16_t)editDigits[1] * 100
                        + (uint16_t)editDigits[2] * 10
                        +           editDigits[3];
            if (yr < 2000 || yr > 2099) yr = 2025;
            dt.setDate(dt.getDay(), dt.getMonth(), yr);
            break;
        }
    }
}

// Advance to the next sub-mode, committing the current edits first.
static void advanceSettingSubMode() {
    commitEditDigits();
    switch (settingSubMode) {
        case SettingSubMode::TIME_SUB:
            settingSubMode = SettingSubMode::DATE_SUB;
            feedback.setMode(LedId::BLUE, LedMode::BLINK);
            break;
        case SettingSubMode::DATE_SUB:
            settingSubMode = SettingSubMode::YEAR_SUB;
            feedback.setMode(LedId::BLUE, LedMode::ON);
            break;
        case SettingSubMode::YEAR_SUB:
            settingSubMode = SettingSubMode::TIME_SUB;
            feedback.setMode(LedId::BLUE, LedMode::OFF);
            break;
    }
    loadEditDigitsFromClock();
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        tapes.moveTo(i, editDigits[i]);
    }
    selectedTape = 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// ALARM_SETTING_MODE helpers
// ─────────────────────────────────────────────────────────────────────────────

static void loadAlarmEditDigits() {
    uint8_t ah = dt.getAlarmHour(), am = dt.getAlarmMinute();
    alarmEditDigits[0] = ah / 10; alarmEditDigits[1] = ah % 10;
    alarmEditDigits[2] = am / 10; alarmEditDigits[3] = am % 10;
}

static void commitAlarmEditDigits() {
    uint8_t ah = alarmEditDigits[0] * 10 + alarmEditDigits[1];
    uint8_t am = alarmEditDigits[2] * 10 + alarmEditDigits[3];
    if (ah > 23) ah = 23;
    if (am > 59) am = 59;
    dt.setAlarm(ah, am);
}

// ─────────────────────────────────────────────────────────────────────────────
// Mode transitions
// ─────────────────────────────────────────────────────────────────────────────

static void enterMode(AppMode next);

// Commit any pending edits when leaving a setup mode.
static void exitCurrentMode() {
    switch (appMode) {
        case AppMode::SETTING_MODE:
            commitEditDigits();
            break;
        case AppMode::ALARM_SETTING_MODE:
            commitAlarmEditDigits();
            break;
        case AppMode::TAPE_ADJUST_MODE:
            tapes.setZeroPoint();
            break;
        default:
            break;
    }
}

static void enterMode(AppMode next) {
    exitCurrentMode();
    appMode = next;
    feedback.setModeDisplay(static_cast<uint8_t>(next));
    touchInactivity();

    switch (next) {

        case AppMode::BASE_MODE:
            alarmRinging = false;
            feedback.setBuzzerActive(false);
            feedback.setMode(LedId::BLUE, snoozeActive ? LedMode::ON : LedMode::OFF);
            feedback.setMode(LedId::RED,  dt.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
            dt.resumeTracking();
            // Refresh tapes with current time.
            tapes.moveTo(0, dt.getHours()   / 10);
            tapes.moveTo(1, dt.getHours()   % 10);
            tapes.moveTo(2, dt.getMinutes() / 10);
            tapes.moveTo(3, dt.getMinutes() % 10);
            Serial.println(F("→ BASE_MODE"));
            break;

        case AppMode::SETTING_MODE:
            settingSubMode = SettingSubMode::TIME_SUB;
            selectedTape   = 0;
            feedback.setMode(LedId::BLUE, LedMode::OFF);  // OFF = Time sub-mode
            feedback.setMode(LedId::RED,  LedMode::OFF);
            loadEditDigitsFromClock();
            for (uint8_t i = 0; i < TAPE_COUNT; i++) tapes.moveTo(i, editDigits[i]);
            Serial.println(F("→ SETTING_MODE"));
            break;

        case AppMode::ALARM_SETTING_MODE:
            alarmSelectedTape = 0;
            feedback.setMode(LedId::BLUE, LedMode::OFF);
            feedback.setMode(LedId::RED,  LedMode::OFF);
            loadAlarmEditDigits();
            for (uint8_t i = 0; i < TAPE_COUNT; i++) tapes.moveTo(i, alarmEditDigits[i]);
            Serial.println(F("→ ALARM_SETTING_MODE"));
            break;

        case AppMode::TAPE_ADJUST_MODE:
            calibTape = 0;
            dt.pauseTracking();
            feedback.setMode(LedId::BLUE, LedMode::OFF);
            feedback.setMode(LedId::RED,  LedMode::OFF);
            Serial.print(F("→ TAPE_ADJUST_MODE  tape="));
            Serial.println(calibTape);
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
    dt.begin();
    input.begin();
    feedback.begin();

    enterMode(AppMode::BASE_MODE);
}

// ─────────────────────────────────────────────────────────────────────────────
// loop
// ─────────────────────────────────────────────────────────────────────────────

void loop() {
    // ── Tick all subsystems ───────────────────────────────────────────────────
    tapes.update();
    dt.update();
    input.update();
    feedback.update();

    // ── Snooze expiry check ───────────────────────────────────────────────────
    if (snoozeActive && (millis() >= snoozeUntilMs)) {
        snoozeActive = false;
        alarmRinging = true;
        feedback.setModeDisplay(static_cast<uint8_t>(appMode));
        feedback.setMode(LedId::BLUE, LedMode::OFF);
        feedback.setMode(LedId::RED,  LedMode::FLASH);
        feedback.setBuzzerActive(true);
        Serial.println(F("Snooze expired → ALARM"));
    }

    // ── Propagate time changes to tapes in BASE_MODE ──────────────────────────
    if (appMode == AppMode::BASE_MODE && !alarmRinging && !snoozeActive) {
        bool hc = dt.consumeHoursChanged();
        bool mc = dt.consumeMinutesChanged();
        if (hc || mc) {
            tapes.moveTo(0, dt.getHours()   / 10);
            tapes.moveTo(1, dt.getHours()   % 10);
            tapes.moveTo(2, dt.getMinutes() / 10);
            tapes.moveTo(3, dt.getMinutes() % 10);
        }
        if (dt.consumeAlarmFired()) {
            alarmRinging = true;
            feedback.setMode(LedId::GREEN_1, LedMode::FLASH);
            feedback.setMode(LedId::GREEN_2, LedMode::FLASH);
            feedback.setMode(LedId::RED,     LedMode::FLASH);
            feedback.setBuzzerActive(true);
            Serial.println(F("ALARM RINGING"));
        }
    }

    // ── Read button events ────────────────────────────────────────────────────
    ButtonEvent evMode     = input.getEvent(BtnId::MODE);
    ButtonEvent evInc      = input.getEvent(BtnId::INC);
    ButtonEvent evNextTape = input.getEvent(BtnId::NEXT_TAPE);
    ButtonEvent evAlarm    = input.getEvent(BtnId::ALARM_TOGGLE);

    // ── State machine ─────────────────────────────────────────────────────────
    switch (appMode) {

        // ── BASE_MODE ─────────────────────────────────────────────────────────
        case AppMode::BASE_MODE:
            if (alarmRinging) {
                if (evAlarm == ButtonEvent::SHORT_PRESS) {
                    // Activate snooze: pause alarm temporarily.
                    dt.dismissAlarm();
                    alarmRinging  = false;
                    snoozeActive  = true;
                    snoozeUntilMs = millis() + SNOOZE_DURATION_MS;
                    feedback.setBuzzerActive(false);
                    feedback.setModeDisplay(static_cast<uint8_t>(appMode));
                    feedback.setMode(LedId::BLUE, LedMode::ON);
                    feedback.setMode(LedId::RED,  dt.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.println(F("Snooze activated"));
                } else if (evMode     == ButtonEvent::SHORT_PRESS ||
                           evInc      == ButtonEvent::SHORT_PRESS ||
                           evNextTape == ButtonEvent::SHORT_PRESS) {
                    // Dismiss alarm permanently.
                    dt.dismissAlarm();
                    alarmRinging = false;
                    feedback.setBuzzerActive(false);
                    feedback.setModeDisplay(static_cast<uint8_t>(appMode));
                    feedback.setMode(LedId::RED, dt.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.println(F("Alarm dismissed"));
                }
            } else {
                if (evMode == ButtonEvent::SHORT_PRESS) {
                    enterMode(AppMode::SETTING_MODE);
                } else if (evAlarm == ButtonEvent::SHORT_PRESS) {
                    dt.toggleAlarmEnabled();
                    feedback.setMode(LedId::RED, dt.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.print(F("Alarm "));
                    Serial.println(dt.isAlarmEnabled() ? F("ON") : F("OFF"));
                }
            }
            break;

        // ── SETTING_MODE ──────────────────────────────────────────────────────
        case AppMode::SETTING_MODE:
            if (evMode == ButtonEvent::SHORT_PRESS) {
                enterMode(AppMode::ALARM_SETTING_MODE);
            } else if (inactivityExpired()) {
                enterMode(AppMode::BASE_MODE);
            } else if (evNextTape == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                if (selectedTape < TAPE_COUNT - 1) {
                    selectedTape++;
                } else {
                    selectedTape = 0;
                    advanceSettingSubMode();
                }
            } else if (evInc == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                editDigits[selectedTape] = (editDigits[selectedTape] + 1) % TAPE_DIGITS;
                tapes.moveTo(selectedTape, editDigits[selectedTape]);
            }
            break;

        // ── ALARM_SETTING_MODE ────────────────────────────────────────────────
        case AppMode::ALARM_SETTING_MODE:
            if (evMode == ButtonEvent::SHORT_PRESS) {
                enterMode(AppMode::TAPE_ADJUST_MODE);
            } else if (inactivityExpired()) {
                enterMode(AppMode::BASE_MODE);
            } else if (evNextTape == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                alarmSelectedTape = (alarmSelectedTape + 1) % TAPE_COUNT;
            } else if (evInc == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                alarmEditDigits[alarmSelectedTape] =
                    (alarmEditDigits[alarmSelectedTape] + 1) % TAPE_DIGITS;
                tapes.moveTo(alarmSelectedTape, alarmEditDigits[alarmSelectedTape]);
            }
            break;

        // ── TAPE_ADJUST_MODE ──────────────────────────────────────────────────
        case AppMode::TAPE_ADJUST_MODE:
            if (evMode == ButtonEvent::SHORT_PRESS) {
                // exitCurrentMode() inside enterMode() calls setZeroPoint().
                enterMode(AppMode::BASE_MODE);
            } else if (evNextTape == ButtonEvent::SHORT_PRESS) {
                calibTape = (calibTape + 1) % TAPE_COUNT;
                Serial.print(F("TAPE_ADJUST  tape="));
                Serial.println(calibTape);
            } else if (evInc == ButtonEvent::SHORT_PRESS) {
                tapes.nudge(calibTape, 1);
            }
            break;
    }
}