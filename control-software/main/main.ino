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
//     BTN_MODE short       → SETTING_MODE
//     BTN_ALARM_TOGGLE     → toggle alarm on/off (Red LED reflects state)
//     alarm fires          → alarm ringing (Green+Red LEDs flash rapidly)
//       BTN_ALARM_TOGGLE   → snooze for SNOOZE_DURATION_MS (Blue LED on)
//       BTN_MODE / BTN_INC / BTN_NEXT_TAPE → dismiss alarm permanently
//
//   SETTING_MODE (01)
//     BTN_NEXT_TAPE cycles the active field: Hours → Minutes → Day → Month → Year
//     Yellow LED on = Time field (Hours/Minutes) active
//     Blue   LED on = Date field (Day/Month) active
//     Red    LED on = Year field active
//     BTN_INC       → increment selected field; tapes jog to show new value
//     BTN_MODE      → save time+date → ALARM_SETTING_MODE
//     Inactivity    → save time+date → BASE_MODE
//
//   ALARM_SETTING_MODE (10)
//     Tapes show current alarm time.
//     BTN_NEXT_TAPE → toggle between Hour (Yellow on) and Minute (Blue on)
//     BTN_INC       → increment selected alarm field; tapes jog
//     BTN_MODE      → save alarm → TAPE_ADJUST_MODE
//     Inactivity    → save alarm → BASE_MODE
//
//   TAPE_ADJUST_MODE (11)
//     Time tracking paused.  Tapes stay at current physical position.
//     BTN_NEXT_TAPE → select next tape/motor (cycles 0–3)
//     BTN_INC       → nudge selected motor +1 raw step for fine-tuning
//     BTN_MODE      → setZeroPoint() on all tapes → BASE_MODE
// ─────────────────────────────────────────────────────────────────────────────

#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_MCP23X17.h>

#include "Config.h"
#include "TapeControl.h"
#include "DateTimeControl.h"
#include "InputControl.h"
#include "LedControl.h"

// ── Application modes ─────────────────────────────────────────────────────────
enum class AppMode : uint8_t {
    BASE_MODE          = 0,   // 00 – display time
    SETTING_MODE       = 1,   // 01 – set time / date / year
    ALARM_SETTING_MODE = 2,   // 10 – set alarm time
    TAPE_ADJUST_MODE   = 3    // 11 – manual tape calibration
};

// ── Setting sub-fields (used in SETTING_MODE) ─────────────────────────────────
enum class SettingField : uint8_t { HOURS, MINUTES, DAY, MONTH, YEAR };

// ── Alarm setting sub-fields (used in ALARM_SETTING_MODE) ─────────────────────
enum class AlarmField : uint8_t { HOUR, MINUTE };

// ── Subsystem objects ─────────────────────────────────────────────────────────
Adafruit_MCP23X17 mcp;
TapeControl       tapes(mcp);
DateTimeControl   clock;
InputControl      input(BTN_PINS);
LedControl        leds(LED_PINS);

// ── State variables ───────────────────────────────────────────────────────────
static AppMode       appMode       = AppMode::BASE_MODE;
static bool          alarmRinging  = false;
static bool          snoozeActive  = false;
static unsigned long snoozeUntilMs = 0;
static SettingField  settingField  = SettingField::HOURS;
static AlarmField    alarmField    = AlarmField::HOUR;
static uint8_t       calibTape     = 0;
static unsigned long inactivityMs  = 0;

// ─────────────────────────────────────────────────────────────────────────────
// LED helpers
// ─────────────────────────────────────────────────────────────────────────────

// Reflect current mode as a 2-bit binary value on the Green LED pair.
static void updateModeLeds() {
    uint8_t val = static_cast<uint8_t>(appMode);
    leds.setMode(LedId::GREEN_1, (val & 0x01) ? LedMode::ON : LedMode::OFF);
    leds.setMode(LedId::GREEN_2, (val & 0x02) ? LedMode::ON : LedMode::OFF);
}

// Light Yellow, Blue, or Red to show the active setting sub-field group.
static void updateSettingFieldLeds() {
    bool isTime = (settingField == SettingField::HOURS   ||
                   settingField == SettingField::MINUTES);
    bool isDate = (settingField == SettingField::DAY     ||
                   settingField == SettingField::MONTH);
    bool isYear = (settingField == SettingField::YEAR);
    leds.setMode(LedId::YELLOW, isTime ? LedMode::ON : LedMode::OFF);
    leds.setMode(LedId::BLUE,   isDate ? LedMode::ON : LedMode::OFF);
    leds.setMode(LedId::RED,    isYear ? LedMode::ON : LedMode::OFF);
}

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

// Jog tapes to show the value for the current setting sub-field.
// Time fields: left pair (HH) or right pair (MM); date/year similarly.
static void displaySettingField() {
    switch (settingField) {
        case SettingField::HOURS: {
            uint8_t h = clock.getHours();
            tapes.moveTo(0, h / 10); tapes.moveTo(1, h % 10);
            tapes.moveTo(2, 0);      tapes.moveTo(3, 0);
            break;
        }
        case SettingField::MINUTES: {
            uint8_t m = clock.getMinutes();
            tapes.moveTo(0, 0);      tapes.moveTo(1, 0);
            tapes.moveTo(2, m / 10); tapes.moveTo(3, m % 10);
            break;
        }
        case SettingField::DAY: {
            uint8_t d = clock.getDay();
            tapes.moveTo(0, 0); tapes.moveTo(1, 0);
            tapes.moveTo(2, d / 10); tapes.moveTo(3, d % 10);
            break;
        }
        case SettingField::MONTH: {
            uint8_t mo = clock.getMonth();
            tapes.moveTo(0, 0); tapes.moveTo(1, 0);
            tapes.moveTo(2, mo / 10); tapes.moveTo(3, mo % 10);
            break;
        }
        case SettingField::YEAR: {
            uint16_t yr = clock.getYear();
            tapes.moveTo(0, 2);                              // '20YY' century prefix: tens = 2
            tapes.moveTo(1, 0);                              // '20YY' century prefix: ones = 0
            tapes.moveTo(2, (uint8_t)((yr % 100) / 10));
            tapes.moveTo(3, (uint8_t)((yr % 100) % 10));
            break;
        }
    }
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
// Mode transitions
// ─────────────────────────────────────────────────────────────────────────────

// Forward declaration needed by enterMode().
static void enterMode(AppMode next);

// Persist state and perform any cleanup when leaving the current mode.
static void exitCurrentMode() {
    switch (appMode) {
        case AppMode::SETTING_MODE:
            clock.saveTime();
            clock.saveDate();
            break;
        case AppMode::ALARM_SETTING_MODE:
            clock.saveAlarm();
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
    updateModeLeds();
    touchInactivity();

    switch (next) {

        case AppMode::BASE_MODE:
            alarmRinging = false;
            leds.setMode(LedId::YELLOW, LedMode::OFF);
            leds.setMode(LedId::BLUE,   snoozeActive ? LedMode::ON : LedMode::OFF);
            leds.setMode(LedId::RED,    clock.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
            clock.resumeTracking();
            displayCurrentTime();
            Serial.println(F("→ BASE_MODE"));
            break;

        case AppMode::SETTING_MODE:
            settingField = SettingField::HOURS;
            updateSettingFieldLeds();
            displaySettingField();
            Serial.println(F("→ SETTING_MODE"));
            break;

        case AppMode::ALARM_SETTING_MODE:
            alarmField = AlarmField::HOUR;
            leds.setMode(LedId::YELLOW, LedMode::ON);   // Hour selected
            leds.setMode(LedId::BLUE,   LedMode::OFF);
            leds.setMode(LedId::RED,    LedMode::OFF);
            displayAlarmTime();
            Serial.println(F("→ ALARM_SETTING_MODE"));
            break;

        case AppMode::TAPE_ADJUST_MODE:
            calibTape = 0;
            clock.pauseTracking();
            leds.setMode(LedId::YELLOW, LedMode::OFF);
            leds.setMode(LedId::BLUE,   LedMode::OFF);
            leds.setMode(LedId::RED,    LedMode::OFF);
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
    tapes.loadCalibration();
    clock.begin();   // Loads time / date / alarm from EEPROM
    input.begin();
    leds.begin();

    enterMode(AppMode::BASE_MODE);
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

    // ── Snooze expiry check ───────────────────────────────────────────────────
    if (snoozeActive && (millis() >= snoozeUntilMs)) {
        snoozeActive = false;
        alarmRinging = true;
        // Flash Green pair and Red to signal re-fired alarm.
        leds.setMode(LedId::GREEN_1, LedMode::FLASH);
        leds.setMode(LedId::GREEN_2, LedMode::FLASH);
        leds.setMode(LedId::BLUE,    LedMode::OFF);
        leds.setMode(LedId::RED,     LedMode::FLASH);
        Serial.println(F("Snooze expired → ALARM"));
    }

    // ── Propagate time changes to tapes in BASE_MODE ──────────────────────────
    if (appMode == AppMode::BASE_MODE && !alarmRinging && !snoozeActive) {
        bool hc = clock.consumeHoursChanged();
        bool mc = clock.consumeMinutesChanged();
        if (hc || mc) {
            displayCurrentTime();
        }
        if (clock.consumeAlarmFired()) {
            alarmRinging = true;
            leds.setMode(LedId::GREEN_1, LedMode::FLASH);
            leds.setMode(LedId::GREEN_2, LedMode::FLASH);
            leds.setMode(LedId::RED,     LedMode::FLASH);
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
                    // Activate snooze: dismiss alarm temporarily.
                    clock.dismissAlarm();
                    alarmRinging  = false;
                    snoozeActive  = true;
                    snoozeUntilMs = millis() + SNOOZE_DURATION_MS;
                    updateModeLeds();   // Restore Green LEDs to steady 00
                    leds.setMode(LedId::BLUE, LedMode::ON);
                    leds.setMode(LedId::RED,  clock.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.println(F("Snooze activated"));
                } else if (evMode     == ButtonEvent::SHORT_PRESS ||
                           evInc      == ButtonEvent::SHORT_PRESS ||
                           evNextTape == ButtonEvent::SHORT_PRESS) {
                    // Dismiss alarm permanently.
                    clock.dismissAlarm();
                    alarmRinging = false;
                    updateModeLeds();
                    leds.setMode(LedId::RED, clock.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.println(F("Alarm dismissed"));
                }
            } else {
                if (evMode == ButtonEvent::SHORT_PRESS) {
                    enterMode(AppMode::SETTING_MODE);
                } else if (evAlarm == ButtonEvent::SHORT_PRESS) {
                    clock.toggleAlarmEnabled();
                    clock.saveAlarm();
                    leds.setMode(LedId::RED, clock.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.print(F("Alarm "));
                    Serial.println(clock.isAlarmEnabled() ? F("ON") : F("OFF"));
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
                switch (settingField) {
                    case SettingField::HOURS:   settingField = SettingField::MINUTES; break;
                    case SettingField::MINUTES: settingField = SettingField::DAY;     break;
                    case SettingField::DAY:     settingField = SettingField::MONTH;   break;
                    case SettingField::MONTH:   settingField = SettingField::YEAR;    break;
                    case SettingField::YEAR:    settingField = SettingField::HOURS;   break;
                }
                updateSettingFieldLeds();
                displaySettingField();
            } else if (evInc == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                switch (settingField) {
                    case SettingField::HOURS:   clock.incrementHours();   break;
                    case SettingField::MINUTES: clock.incrementMinutes(); break;
                    case SettingField::DAY:     clock.incrementDay();     break;
                    case SettingField::MONTH:   clock.incrementMonth();   break;
                    case SettingField::YEAR:    clock.incrementYear();    break;
                }
                displaySettingField();
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
                alarmField = (alarmField == AlarmField::HOUR)
                             ? AlarmField::MINUTE
                             : AlarmField::HOUR;
                leds.setMode(LedId::YELLOW,
                             (alarmField == AlarmField::HOUR) ? LedMode::ON : LedMode::OFF);
                leds.setMode(LedId::BLUE,
                             (alarmField == AlarmField::MINUTE) ? LedMode::ON : LedMode::OFF);
            } else if (evInc == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                if (alarmField == AlarmField::HOUR) clock.incrementAlarmHour();
                else                                clock.incrementAlarmMinute();
                displayAlarmTime();
            }
            break;

        // ── TAPE_ADJUST_MODE ──────────────────────────────────────────────────
        case AppMode::TAPE_ADJUST_MODE:
            if (evMode == ButtonEvent::SHORT_PRESS) {
                // exitCurrentMode() called inside enterMode() will call setZeroPoint().
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