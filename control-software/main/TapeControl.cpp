#include "TapeControl.h"
#include <EEPROM.h>

// Full-step sequence nibble per phase (forward).
// Each nibble encodes which two coils are energised for one motor:
//   bit3=IN4, bit2=IN3, bit1=IN2, bit0=IN1
// Phase 0: IN4+IN3 = 0b1100 = 0xC  → original 0xCCCC pattern
// Phase 1: IN3+IN2 = 0b0110 = 0x6  → original 0x6666 pattern
// Phase 2: IN2+IN1 = 0b0011 = 0x3  → original 0x3333 pattern
// Phase 3: IN4+IN1 = 0b1001 = 0x9  → original 0x9999 pattern
const uint8_t TapeControl::PHASE_NIBBLE[STEP_PHASES] = { 0xC, 0x6, 0x3, 0x9 };

// ─────────────────────────────────────────────────────────────────────────────

TapeControl::TapeControl(Adafruit_MCP23X17& mcp)
    : _mcp(mcp), _lastStepMs(0)
{
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        _stepsRemaining[i] = 0;
        _phase[i]          = 0;
        _currentDigit[i]   = 0;
    }
}

void TapeControl::begin() {
    for (uint8_t i = 0; i < 16; i++) {
        _mcp.pinMode(i, OUTPUT);
    }
    // Deenergise all coils at startup.
    _mcp.writeGPIOAB(0x0000);
}

void TapeControl::moveTo(uint8_t tapeIndex, uint8_t digit) {
    if (tapeIndex >= TAPE_COUNT || digit >= TAPE_DIGITS) return;
    if (_currentDigit[tapeIndex] == digit) return;

    int8_t diff = (int8_t)digit - (int8_t)_currentDigit[tapeIndex];
    _stepsRemaining[tapeIndex] = (int16_t)diff * STEPS_PER_DIGIT;
    _currentDigit[tapeIndex]   = digit;
}

void TapeControl::nudge(uint8_t tapeIndex, int16_t steps) {
    if (tapeIndex >= TAPE_COUNT) return;
    _stepsRemaining[tapeIndex] += steps;
}

void TapeControl::resetDigit(uint8_t tapeIndex, uint8_t digit) {
    if (tapeIndex >= TAPE_COUNT || digit >= TAPE_DIGITS) return;
    _currentDigit[tapeIndex] = digit;
}

// Mark all current physical tape positions as digit 0 and persist.
void TapeControl::setZeroPoint() {
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        _currentDigit[i]   = 0;
        _stepsRemaining[i] = 0;
    }
    saveCalibration();
}

void TapeControl::saveCalibration() {
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        EEPROM.update(EEPROM_TAPE_BASE + i, _currentDigit[i]);
    }
}

void TapeControl::loadCalibration() {
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        uint8_t d = EEPROM.read(EEPROM_TAPE_BASE + i);
        _currentDigit[i] = (d < TAPE_DIGITS) ? d : 0;
    }
}

void TapeControl::update() {
    if (!isBusy()) return;

    unsigned long now = millis();
    if (now - _lastStepMs < STEP_INTERVAL_MS) return;
    _lastStepMs = now;

    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        if (_stepsRemaining[i] > 0) {
            advancePhase(i);
            _stepsRemaining[i]--;
        } else if (_stepsRemaining[i] < 0) {
            retreatPhase(i);
            _stepsRemaining[i]++;
        }
    }

    applyPhases();
}

bool TapeControl::isBusy() const {
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        if (_stepsRemaining[i] != 0) return true;
    }
    return false;
}

// ── Private helpers ───────────────────────────────────────────────────────────

void TapeControl::advancePhase(uint8_t idx) {
    _phase[idx] = (_phase[idx] + 1) % STEP_PHASES;
}

void TapeControl::retreatPhase(uint8_t idx) {
    _phase[idx] = (_phase[idx] == 0) ? (STEP_PHASES - 1) : (_phase[idx] - 1);
}

void TapeControl::applyPhases() {
    _mcp.writeGPIOAB(buildGPIOAB());
}

uint16_t TapeControl::buildGPIOAB() const {
    uint16_t gpio = 0;
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        if (_stepsRemaining[i] != 0) {
            // Each motor occupies a 4-bit nibble; motor i starts at bit i*4.
            gpio |= (uint16_t)PHASE_NIBBLE[_phase[i]] << (i * 4);
        }
        // Idle motors keep their nibble as 0 → coils deenergised.
    }
    return gpio;
}
