#include <Wire.h>
#include <Adafruit_MCP23X17.h>

#include "Config.h"
#include "TapeControl.h"
#include "DateTimeControl.h"
#include "InputControl.h"
#include "FeedbackControl.h"

// ── Application modes ─────────────────────────────────────────────────────────
enum class AppMode : uint8_t {
    BASE_MODE          = 0,   // 00 – Default Time
    ALARM_SETTING_MODE = 1,   // 01 – Set Alarm
    SETTING_MODE       = 2,   // 10 – Set Time/Date/Year
    TAPE_ADJUST_MODE   = 3    // 11 – Tape Calibration
};

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

// Global Illumination, Date, & Direction State
static bool           lightActive        = false;
static unsigned long  lightTurnedOnMs    = 0;
static unsigned long  lastOnPressMs      = 0;
static bool           showingDate        = false;
static bool           wasShowingDate     = false;
static unsigned long  dateDisplayStartMs = 0;
static bool           isReversed         = false; 

// SETTING_MODE state
static SettingSubMode settingSubMode = SettingSubMode::TIME_SUB;
static uint8_t        selectedTape   = 0;
static uint8_t        editDigits[TAPE_COUNT];

// ALARM_SETTING_MODE state
static uint8_t        alarmSelectedTape = 0;
static uint8_t        alarmEditDigits[TAPE_COUNT];

// TAPE_ADJUST_MODE state
static uint8_t        calibTape = 0;
static bool           isFastNudging = false; 
static unsigned long  lastNudgeMs   = 0;     

// ── Debug helper function ────────────────────────────────────────────────────
static void printDisplayState(const char* context) {
    Serial.print(F("[DEBUG - "));
    Serial.print(context);
    Serial.print(F("] MODE: "));
    
    switch (appMode) {
        case AppMode::BASE_MODE:
            if (showingDate) Serial.print(F("0 (BASE - SHOWING DATE)"));
            else Serial.print(F("0 (BASE - SHOWING TIME)"));
            break;
        case AppMode::ALARM_SETTING_MODE:
            Serial.print(F("1 (ALARM SETTING)"));
            break;
        case AppMode::SETTING_MODE:
            Serial.print(F("2 (CLOCK SETTING - "));
            if (settingSubMode == SettingSubMode::TIME_SUB) Serial.print(F("TIME SUB)"));
            else if (settingSubMode == SettingSubMode::DATE_SUB) Serial.print(F("DATE SUB)"));
            else if (settingSubMode == SettingSubMode::YEAR_SUB) Serial.print(F("YEAR SUB)"));
            break;
        case AppMode::TAPE_ADJUST_MODE:
            Serial.print(F("3 (TAPE ADJUST / CALIBRATION)"));
            break;
    }
    
    Serial.print(F(" | DIRECTION: "));
    Serial.print(isReversed ? F("REVERSE (-)") : F("FORWARD (+)"));

    Serial.print(F(" | SHOULD DISPLAY ON SCREEN: ["));
    
    if (appMode == AppMode::BASE_MODE) {
        if (showingDate) {
            Serial.print(dt.getDay() / 10);   Serial.print(dt.getDay() % 10);   Serial.print(F(":"));
            Serial.print(dt.getMonth() / 10); Serial.print(dt.getMonth() % 10);
        } else {
            Serial.print(dt.getHours() / 10);   Serial.print(dt.getHours() % 10);   Serial.print(F(":"));
            Serial.print(dt.getMinutes() / 10); Serial.print(dt.getMinutes() % 10);
        }
    } else if (appMode == AppMode::ALARM_SETTING_MODE) {
        for(int i=0; i<4; i++) {
            if (i == alarmSelectedTape) Serial.print(F(">"));
            Serial.print(alarmEditDigits[i]);
            if (i == alarmSelectedTape) Serial.print(F("<"));
            if (i == 1) Serial.print(F(":"));
        }
    } else if (appMode == AppMode::SETTING_MODE) {
        for(int i=0; i<4; i++) {
            if (i == selectedTape) Serial.print(F(">"));
            Serial.print(editDigits[i]);
            if (i == selectedTape) Serial.print(F("<"));
            if (i == 1) Serial.print(F(":"));
        }
    } else if (appMode == AppMode::TAPE_ADJUST_MODE) {
        Serial.print(F("NUDGING TAPE: ")); Serial.print(calibTape);
    }
    
    Serial.println(F("]"));
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers (Inactivity, Setting Loads)
// ─────────────────────────────────────────────────────────────────────────────
static void touchInactivity() { inactivityMs = millis(); }
static bool inactivityExpired() { return (millis() - inactivityMs) >= SET_MODE_TIMEOUT_MS; }

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

static void commitEditDigits() {
    switch (settingSubMode) {
        case SettingSubMode::TIME_SUB: {
            uint8_t h = editDigits[0] * 10 + editDigits[1];
            uint8_t m = editDigits[2] * 10 + editDigits[3];
            if (h > 23) h = 23; if (m > 59) m = 59;
            dt.setTime(h, m);
            break;
        }
        case SettingSubMode::DATE_SUB: {
            uint8_t d  = editDigits[0] * 10 + editDigits[1];
            uint8_t mo = editDigits[2] * 10 + editDigits[3];
            if (mo < 1 || mo > 12) mo = 1; if (d < 1 || d > 31) d = 1;
            dt.setDate(d, mo, dt.getYear());
            break;
        }
        case SettingSubMode::YEAR_SUB: {
            uint16_t yr = editDigits[0]*1000 + editDigits[1]*100 + editDigits[2]*10 + editDigits[3];
            if (yr < 0000 || yr > 9999) yr = 2000;
            dt.setDate(dt.getDay(), dt.getMonth(), yr);
            break;
        }
    }
}

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
    for (uint8_t i = 0; i < TAPE_COUNT; i++) tapes.moveTo(i, editDigits[i]);
    printDisplayState("SUBMODE CHANGE");
}

static void loadAlarmEditDigits() {
    uint8_t ah = dt.getAlarmHour(), am = dt.getAlarmMinute();
    alarmEditDigits[0] = ah / 10; alarmEditDigits[1] = ah % 10;
    alarmEditDigits[2] = am / 10; alarmEditDigits[3] = am % 10;
}

static void commitAlarmEditDigits() {
    uint8_t ah = alarmEditDigits[0] * 10 + alarmEditDigits[1];
    uint8_t am = alarmEditDigits[2] * 10 + alarmEditDigits[3];
    if (ah > 23) ah = 23; if (am > 59) am = 59;
    dt.setAlarm(ah, am);
}

// ─────────────────────────────────────────────────────────────────────────────
// Mode transitions
// ─────────────────────────────────────────────────────────────────────────────
static void exitCurrentMode() {
    switch (appMode) {
        case AppMode::SETTING_MODE:       commitEditDigits(); break;
        case AppMode::ALARM_SETTING_MODE: commitAlarmEditDigits(); break;
        // The setZeroPoint() saves the manually adjusted positions properly on exit
        case AppMode::TAPE_ADJUST_MODE:   tapes.setZeroPoint(); break; 
        default: break;
    }
}

static void enterMode(AppMode next) {
    exitCurrentMode();
    appMode = next;
    
    feedback.setModeDisplay(static_cast<uint8_t>(next));
    touchInactivity();

    if (!alarmRinging) {
        feedback.setMode(LedId::RED, dt.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
    }

    isReversed = false;
    if (next == AppMode::BASE_MODE) {
        feedback.setMode(LedId::YELLOW, lightActive ? LedMode::ON : LedMode::OFF);
    } else {
        feedback.setMode(LedId::YELLOW, LedMode::OFF); 
    }

    switch (next) {
        case AppMode::BASE_MODE:
            alarmRinging = false;
            feedback.setBuzzerActive(false);
            feedback.setMode(LedId::BLUE, snoozeActive ? LedMode::ON : LedMode::OFF);
            dt.resumeTracking();
            break;

        case AppMode::ALARM_SETTING_MODE: 
            alarmSelectedTape = 0;
            feedback.setMode(LedId::BLUE, LedMode::OFF);
            loadAlarmEditDigits();
            for (uint8_t i = 0; i < TAPE_COUNT; i++) tapes.moveTo(i, alarmEditDigits[i]);
            break;

        case AppMode::SETTING_MODE: 
            settingSubMode = SettingSubMode::TIME_SUB;
            selectedTape   = 0;
            feedback.setMode(LedId::BLUE, LedMode::OFF); 
            loadEditDigitsFromClock();
            for (uint8_t i = 0; i < TAPE_COUNT; i++) tapes.moveTo(i, editDigits[i]);
            break;

        case AppMode::TAPE_ADJUST_MODE: 
            calibTape = 0;
            isFastNudging = false; 
            dt.pauseTracking();
            feedback.setMode(LedId::BLUE, LedMode::OFF);
            
            // --- NEW: Automatically tell all tapes to spin to 00:00 ---
            Serial.println(F("[CALIBRATION] Auto-returning all tapes to 00:00..."));
            for (uint8_t i = 0; i < TAPE_COUNT; i++) {
                tapes.moveTo(i, 0);
            }
            break;
    }

    printDisplayState("MODE ENTRY");
}

// ─────────────────────────────────────────────────────────────────────────────
// setup
// ─────────────────────────────────────────────────────────────────────────────
void setup() {
    Serial.begin(9600);
    Serial.println(F("--- CHRONOTAPE BOOTING ---"));
    if (!mcp.begin_I2C()) {
        Serial.println(F("Error: MCP23017 not found. Check wiring."));
        while (1);
    }

    tapes.begin();
    dt.begin();
    input.begin();
    feedback.begin();

    for (uint8_t i = 0; i < 4; i++) {
        pinMode(ILLUM_LED_PINS[i], OUTPUT);
        digitalWrite(ILLUM_LED_PINS[i], LOW);
    }

    enterMode(AppMode::BASE_MODE);
}

// ─────────────────────────────────────────────────────────────────────────────
// loop
// ─────────────────────────────────────────────────────────────────────────────
void loop() {
    tapes.update();
    dt.update();
    input.update();
    feedback.update();

    unsigned long now = millis();

    // ── Global ON Button & Light Timer Logic ──────────────────────────────────
    ButtonEvent evOn = input.getEvent(BtnId::ON);

    if (evOn == ButtonEvent::SHORT_PRESS) {
        lightActive = true;
        lightTurnedOnMs = now;
        for (uint8_t i = 0; i < 4; i++) digitalWrite(ILLUM_LED_PINS[i], HIGH);

        if (appMode == AppMode::BASE_MODE) {
            feedback.setMode(LedId::YELLOW, LedMode::ON);
            if (now - lastOnPressMs <= 2000) {
                showingDate = true;
                dateDisplayStartMs = now;
                printDisplayState("DATE DOUBLE-CLICK");
            }
            lastOnPressMs = now;
        } else {
            isReversed = !isReversed;
            feedback.setMode(LedId::YELLOW, isReversed ? LedMode::ON : LedMode::OFF);
            printDisplayState("DIRECTION TOGGLE");
        }
    }

    if (lightActive && (now - lightTurnedOnMs >= 10000)) {
        lightActive = false;
        for (uint8_t i = 0; i < 4; i++) digitalWrite(ILLUM_LED_PINS[i], LOW);
        if (appMode == AppMode::BASE_MODE) {
            feedback.setMode(LedId::YELLOW, LedMode::OFF);
        }
    }

    // ── Snooze expiry ─────────────────────────────────────────────────────────
    if (snoozeActive && (now >= snoozeUntilMs)) {
        snoozeActive = false;
        alarmRinging = true;
        feedback.setModeDisplay(static_cast<uint8_t>(appMode));
        feedback.setMode(LedId::BLUE, LedMode::OFF);
        feedback.setMode(LedId::RED,  LedMode::FLASH);
        feedback.setBuzzerActive(true);
        Serial.println(F("[ALARM] Snooze Expired! Ringing..."));
    }

    // ── Read remaining buttons ────────────────────────────────────────────────
    ButtonEvent evChg  = input.getEvent(BtnId::CHG);
    ButtonEvent evNext = input.getEvent(BtnId::NEXT);
    ButtonEvent evInc  = input.getEvent(BtnId::INC);
    ButtonEvent evSet  = input.getEvent(BtnId::SET);

    // ── State machine ─────────────────────────────────────────────────────────
    switch (appMode) {

        // ── Tryb 0: BASE_MODE ─────────────────────────────────────────────────
        case AppMode::BASE_MODE:
            
            if (showingDate && (now - dateDisplayStartMs >= 5000)) {
                showingDate = false;
                printDisplayState("DATE DISPLAY TIMEOUT");
            }

            if (!alarmRinging && !snoozeActive) {
                bool hc = dt.consumeHoursChanged();
                bool mc = dt.consumeMinutesChanged();
                bool dc = dt.consumeDateChanged();

                if (showingDate) {
                    if (!wasShowingDate || dc) {
                        tapes.moveTo(0, dt.getDay()   / 10);
                        tapes.moveTo(1, dt.getDay()   % 10);
                        tapes.moveTo(2, dt.getMonth() / 10);
                        tapes.moveTo(3, dt.getMonth() % 10);
                        printDisplayState("DATE DISPLAY REFRESH");
                    }
                } else {
                    if (wasShowingDate || hc || mc) {
                        tapes.moveTo(0, dt.getHours()   / 10);
                        tapes.moveTo(1, dt.getHours()   % 10);
                        tapes.moveTo(2, dt.getMinutes() / 10);
                        tapes.moveTo(3, dt.getMinutes() % 10);
                        printDisplayState("TIME CLOCK TICK");
                    }
                }
                wasShowingDate = showingDate;

                if (dt.consumeAlarmFired()) {
                    alarmRinging = true;
                    feedback.setMode(LedId::GREEN_1, LedMode::FLASH);
                    feedback.setMode(LedId::GREEN_2, LedMode::FLASH);
                    feedback.setMode(LedId::RED,     LedMode::FLASH);
                    feedback.setBuzzerActive(true);
                    Serial.println(F("[ALARM] Triggered! Alarm is currently ringing."));
                }
            }

            if (alarmRinging) {
                if (evSet == ButtonEvent::SHORT_PRESS) {
                    dt.dismissAlarm();
                    alarmRinging = false;
                    feedback.setBuzzerActive(false);
                    feedback.setModeDisplay(static_cast<uint8_t>(appMode));
                    feedback.setMode(LedId::RED, dt.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.println(F("[ALARM] Permanently Dismissed via SET button."));
                } 
                else if (evChg == ButtonEvent::SHORT_PRESS || evNext == ButtonEvent::SHORT_PRESS || 
                           evInc == ButtonEvent::SHORT_PRESS || evOn == ButtonEvent::SHORT_PRESS) {
                    dt.dismissAlarm(); 
                    alarmRinging  = false;
                    snoozeActive  = true;
                    snoozeUntilMs = now + SNOOZE_DURATION_MS;
                    feedback.setBuzzerActive(false);
                    feedback.setModeDisplay(static_cast<uint8_t>(appMode));
                    feedback.setMode(LedId::BLUE, LedMode::ON);
                    Serial.println(F("[ALARM] Snoozed for 5 minutes."));
                }
            } else {
                if (evChg == ButtonEvent::SHORT_PRESS) {
                    enterMode(AppMode::ALARM_SETTING_MODE); 
                } else if (evSet == ButtonEvent::SHORT_PRESS) {
                    dt.toggleAlarmEnabled();
                    feedback.setMode(LedId::RED, dt.isAlarmEnabled() ? LedMode::ON : LedMode::OFF);
                    Serial.print(F("[ALARM] Armed State Toggled. Enabled: "));
                    Serial.println(dt.isAlarmEnabled() ? F("YES") : F("NO"));
                }
            }
            break;

        // ── Tryb 1: ALARM_SETTING_MODE ────────────────────────────────────────
        case AppMode::ALARM_SETTING_MODE:
            if (evChg == ButtonEvent::SHORT_PRESS) {
                enterMode(AppMode::SETTING_MODE); 
            } else if (inactivityExpired()) {
                Serial.println(F("[TIMEOUT] Inactivity detected in Alarm Mode."));
                enterMode(AppMode::BASE_MODE);
            } else if (evNext == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                alarmSelectedTape = (alarmSelectedTape + 1) % TAPE_COUNT;
                printDisplayState("ALARM CURSOR MOVE");
            } else if (evInc == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                uint8_t moveAmt = isReversed ? (TAPE_DIGITS - 1) : 1; 
                alarmEditDigits[alarmSelectedTape] = (alarmEditDigits[alarmSelectedTape] + moveAmt) % TAPE_DIGITS;
                tapes.moveTo(alarmSelectedTape, alarmEditDigits[alarmSelectedTape]);
                printDisplayState("ALARM DIGIT EDIT");
            }
            break;

        // ── Tryb 2: SETTING_MODE (Time -> Date -> Year) ───────────────────────
        case AppMode::SETTING_MODE:
            if (evChg == ButtonEvent::SHORT_PRESS) {
                enterMode(AppMode::TAPE_ADJUST_MODE); 
            } else if (inactivityExpired()) {
                Serial.println(F("[TIMEOUT] Inactivity detected in Settings Mode."));
                enterMode(AppMode::BASE_MODE);
            } else if (evSet == ButtonEvent::SHORT_PRESS) { 
                touchInactivity();
                advanceSettingSubMode(); 
            } else if (evNext == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                selectedTape = (selectedTape + 1) % TAPE_COUNT;
                printDisplayState("CLOCK CURSOR MOVE");
            } else if (evInc == ButtonEvent::SHORT_PRESS) {
                touchInactivity();
                uint8_t moveAmt = isReversed ? (TAPE_DIGITS - 1) : 1;
                editDigits[selectedTape] = (editDigits[selectedTape] + moveAmt) % TAPE_DIGITS;
                tapes.moveTo(selectedTape, editDigits[selectedTape]);
                printDisplayState("CLOCK DIGIT EDIT");
            }
            break;

        // ── Tryb 3: TAPE_ADJUST_MODE ──────────────────────────────────────────
        case AppMode::TAPE_ADJUST_MODE:
            if (evChg == ButtonEvent::SHORT_PRESS) {
                enterMode(AppMode::BASE_MODE);
            } else if (evNext == ButtonEvent::SHORT_PRESS) {
                calibTape = (calibTape + 1) % TAPE_COUNT;
                printDisplayState("CALIBRATION TARGET SWAP");
            } 
            else {
                int nudgeStep = isReversed ? -20 : 20;

                if (evInc == ButtonEvent::SHORT_PRESS) {
                    tapes.nudge(calibTape, nudgeStep);
                    printDisplayState("MANUAL NUDGE CLICK");
                } 
                else if (evInc == ButtonEvent::LONG_PRESS) {
                    isFastNudging = true;
                    lastNudgeMs = now;
                    tapes.nudge(calibTape, nudgeStep);
                    printDisplayState("FAST NUDGE START");
                }

                if (isFastNudging) {
                    if (input.isHeld(BtnId::INC)) {
                        if (now - lastNudgeMs >= 40) { 
                            tapes.nudge(calibTape, nudgeStep);
                            lastNudgeMs = now;
                        }
                    } else {
                        isFastNudging = false; 
                        printDisplayState("FAST NUDGE STOP");
                    }
                }
            }
            break;
    }
}