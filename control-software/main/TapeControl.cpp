#include "TapeControl.h"

// Full-step sequence nibble per phase (forward).
// Each nibble encodes which two coils are energised for one motor:
//   bit3=IN4, bit2=IN3, bit1=IN2, bit0=IN1
// Phase 0: IN4+IN3 = 0b1100 = 0xC  → original 0xCCCC pattern
// Phase 1: IN3+IN2 = 0b0110 = 0x6  → original 0x6666 pattern
// Phase 2: IN2+IN1 = 0b0011 = 0x3  → original 0x3333 pattern
// Phase 3: IN4+IN1 = 0b1001 = 0x9  → original 0x9999 pattern
// 8-Step (Half-Step) sequence nibble per phase (Reversed).
// Flipped the order of the phases to make the motors spin in the opposite direction.
const uint8_t TapeControl::PHASE_NIBBLE[STEP_PHASES] = {
    0x9, 0x1, 0x3, 0x2, 0x6, 0x4, 0xC, 0x8
};

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

    // 1. Calculate the shortest logical path between the OLD target and the NEW target
    int8_t diff = (int8_t)digit - (int8_t)_currentDigit[tapeIndex];
    
    if (diff > TAPE_DIGITS / 2) {
        diff -= TAPE_DIGITS;
    } else if (diff < -(TAPE_DIGITS / 2)) {
        diff += TAPE_DIGITS;
    }
    
    // 2. THE FIX: ADD the new difference to whatever the motor is already doing!
    _stepsRemaining[tapeIndex] += (int16_t)diff * STEPS_PER_DIGIT;
    
    // 3. Update the target tracker
    _currentDigit[tapeIndex] = digit;
}

void TapeControl::nudge(uint8_t tapeIndex, int16_t steps) {
    if (tapeIndex >= TAPE_COUNT) return;
    _stepsRemaining[tapeIndex] += steps;
}

void TapeControl::resetDigit(uint8_t tapeIndex, uint8_t digit) {
    if (tapeIndex >= TAPE_COUNT || digit >= TAPE_DIGITS) return;
    _currentDigit[tapeIndex] = digit;
}

// Mark all current physical tape positions as digit 0.
void TapeControl::setZeroPoint() {
    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        _currentDigit[i]   = 0;
        _stepsRemaining[i] = 0;
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
    
    // NEW: Logical to Physical mapping array.
    // Index 0 (Tape 1) -> Nibble 0 (GPA0-3)
    // Index 1 (Tape 2) -> Nibble 1 (GPA4-7)
    // Index 2 (Tape 3) -> Nibble 3 (GPB4-7)  <-- Redirected!
    // Index 3 (Tape 4) -> Nibble 2 (GPB0-3)  <-- Redirected!
    const uint8_t physicalMap[TAPE_COUNT] = {2,3,1,0};

    for (uint8_t i = 0; i < TAPE_COUNT; i++) {
        if (_stepsRemaining[i] != 0) {
            // Shift the bits using our new physicalMap instead of the direct index 'i'
            gpio |= (uint16_t)PHASE_NIBBLE[_phase[i]] << (physicalMap[i] * 4);
        }
        // Idle motors keep their nibble as 0 → coils deenergised.
    }
    return gpio;
}
