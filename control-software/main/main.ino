// ─────────────────────────────────────────────────────────────────────────────
// Chronotape — main.ino
//
// Orchestrates all subsystems through a simple state machine:
//
//   NORMAL      → display current time; LED breathes
//   SET_HOURS   → short ADJUST: increment hours;   long MODE: → SET_MINUTES
//   SET_MINUTES → short ADJUST: increment minutes; long MODE: → CALIBRATE
//   CALIBRATE   → short ADJUST: nudge selected tape forward one step
//                 short MODE:   confirm tape & advance to the next one
//                               (after tape 3, mark all digits as 0, → NORMAL)
//
// No blocking calls; all timing is managed with millis().
// ─────────────────────────────────────────────────────────────────────────────

#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_MCP23X17.h>

#include "TapeControl.h"
#include "TimeControl.h"
#include "InputControl.h"
#include "LedControl.h"

// ── Pin assignments (change to match your wiring) ─────────────────────────────
static const uint8_t PIN_LED    = 9;  // Must be a PWM-capable pin
static const uint8_t PIN_MODE   = 2;  // MODE push-button (active-low)
static const uint8_t PIN_ADJUST = 3;  // ADJUST push-button (active-low)

// ── State machine ─────────────────────────────────────────────────────────────
enum class AppState : uint8_t {
    NORMAL,
    SET_HOURS,
    SET_MINUTES,
    CALIBRATE
};

// ── Subsystem objects ─────────────────────────────────────────────────────────
Adafruit_MCP23X17 mcp;
TapeControl  tapes(mcp);
TimeControl  clock;
InputControl input(PIN_MODE, PIN_ADJUST);
LedControl   led(PIN_LED);

AppState state       = AppState::NORMAL;
uint8_t  calibTape   = 0; // Which tape is currently being calibrated

// ── Helpers ───────────────────────────────────────────────────────────────────

// Tape index mapping for HH:MM (most-significant digit first):
//   tape 0 = hours tens
//   tape 1 = hours units
//   tape 2 = minutes tens
//   tape 3 = minutes units
static void updateTimeDisplay() {
    uint8_t h = clock.getHours();
    uint8_t m = clock.getMinutes();
    tapes.moveTo(0, h / 10);
    tapes.moveTo(1, h % 10);
    tapes.moveTo(2, m / 10);
    tapes.moveTo(3, m % 10);
}

static void enterState(AppState next) {
    state = next;
    switch (next) {
        case AppState::NORMAL:
            led.setMode(LedMode::BREATHING);
            updateTimeDisplay();
            Serial.println(F("State: NORMAL"));
            break;
        case AppState::SET_HOURS:
            led.setMode(LedMode::ON);
            Serial.println(F("State: SET_HOURS"));
            break;
        case AppState::SET_MINUTES:
            led.setMode(LedMode::DIM);
            Serial.println(F("State: SET_MINUTES"));
            break;
        case AppState::CALIBRATE:
            calibTape = 0;
            led.setMode(LedMode::OFF);
            Serial.print(F("State: CALIBRATE  tape="));
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
    clock.begin(12, 0);   // Start at 12:00; update via SET_HOURS / SET_MINUTES
    input.begin();
    led.begin();

    enterState(AppState::NORMAL);
}

// ─────────────────────────────────────────────────────────────────────────────
// loop
// ─────────────────────────────────────────────────────────────────────────────
void loop() {
    // ── Update all subsystems ─────────────────────────────────────────────────
    tapes.update();
    clock.update();
    input.update();
    led.update();

    // ── Propagate time changes to tapes (NORMAL mode only) ───────────────────
    if (state == AppState::NORMAL) {
        bool hChanged = clock.consumeHoursChanged();
        bool mChanged = clock.consumeMinutesChanged();
        if (hChanged || mChanged) {
            updateTimeDisplay();
        }
    }

    // ── Read button events ────────────────────────────────────────────────────
    ButtonEvent modeEvent   = input.getModeEvent();
    ButtonEvent adjustEvent = input.getAdjustEvent();

    // ── State machine transitions ─────────────────────────────────────────────
    switch (state) {

        case AppState::NORMAL:
            // Long MODE → enter time-setting flow
            if (modeEvent == ButtonEvent::LONG_PRESS) {
                enterState(AppState::SET_HOURS);
            }
            break;

        case AppState::SET_HOURS:
            if (adjustEvent == ButtonEvent::SHORT_PRESS) {
                clock.incrementHours();
                updateTimeDisplay();
            }
            if (modeEvent == ButtonEvent::LONG_PRESS) {
                enterState(AppState::SET_MINUTES);
            }
            break;

        case AppState::SET_MINUTES:
            if (adjustEvent == ButtonEvent::SHORT_PRESS) {
                clock.incrementMinutes();
                updateTimeDisplay();
            }
            if (modeEvent == ButtonEvent::LONG_PRESS) {
                enterState(AppState::CALIBRATE);
            }
            break;

        case AppState::CALIBRATE:
            // Short ADJUST: nudge the current tape forward by one raw step.
            if (adjustEvent == ButtonEvent::SHORT_PRESS) {
                tapes.nudge(calibTape, 1);
            }
            // Short MODE: confirm this tape and move to the next.
            if (modeEvent == ButtonEvent::SHORT_PRESS) {
                tapes.resetDigit(calibTape, 0);
                calibTape++;
                if (calibTape >= TAPE_COUNT) {
                    // All tapes calibrated.
                    calibTape = 0;
                    enterState(AppState::NORMAL);
                } else {
                    Serial.print(F("CALIBRATE  tape="));
                    Serial.println(calibTape);
                }
            }
            // Long MODE: cancel calibration without saving.
            if (modeEvent == ButtonEvent::LONG_PRESS) {
                enterState(AppState::NORMAL);
            }
            break;
    }
}