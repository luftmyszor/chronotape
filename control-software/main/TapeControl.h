#pragma once
#include <Arduino.h>
#include <Adafruit_MCP23X17.h>

// ── Hardware constants ────────────────────────────────────────────────────────
// Number of tape/digit tracks (HH:MM → 4 motors).
constexpr uint8_t  TAPE_COUNT       = 4;
// Digits visible per tape (0–9).
constexpr uint8_t  TAPE_DIGITS      = 10;
// Full-step motor phases use 4 states.
constexpr uint8_t  STEP_PHASES      = 4;

// ── Tuning constants ─────────────────────────────────────────────────────────
// Steps the motor must travel to advance one digit position.
// Measure physically and adjust.
constexpr uint16_t STEPS_PER_DIGIT  = 200;
// Minimum milliseconds between consecutive steps (motor speed limit).
constexpr uint16_t STEP_INTERVAL_MS = 2;

// ─────────────────────────────────────────────────────────────────────────────
// TapeControl
//
// Drives up to TAPE_COUNT stepper motors through a single MCP23017 I/O
// expander.  Each motor occupies one 4-bit nibble of the expander's 16-bit
// GPIO register (motor 0 → bits 3:0, motor 1 → bits 7:4, …).
//
// All movement is non-blocking: moveTo() / nudge() load a step counter; the
// actual pulses are emitted one at a time inside update(), which must be
// called from loop().
// ─────────────────────────────────────────────────────────────────────────────
class TapeControl {
public:
    explicit TapeControl(Adafruit_MCP23X17& mcp);

    // Configure all 16 MCP23017 pins as outputs.
    void begin();

    // Queue a move to display the given digit on the given tape.
    // Calculates the shortest bidirectional path; no-op if already there.
    void moveTo(uint8_t tapeIndex, uint8_t digit);

    // Queue a raw step count (positive = forward, negative = backward).
    // Used by calibration mode for fine positioning.  Does NOT update the
    // stored digit position; call resetDigit() afterwards if needed.
    void nudge(uint8_t tapeIndex, int16_t steps);

    // Forcibly set the logical digit for a tape without moving the motor.
    // Call this after physically aligning a tape during calibration.
    void resetDigit(uint8_t tapeIndex, uint8_t digit);

    // Advance pending steps by one pulse for all active motors.
    // Call every loop iteration.
    void update();

    // True while any motor still has queued steps.
    bool isBusy() const;

private:
    Adafruit_MCP23X17& _mcp;

    int16_t _stepsRemaining[TAPE_COUNT]; // >0 forward, <0 backward, 0 idle
    uint8_t _phase[TAPE_COUNT];          // Current step phase (0–3) per motor
    uint8_t _currentDigit[TAPE_COUNT];   // Logical digit currently displayed

    unsigned long _lastStepMs;           // Timestamp of the last step pulse

    // Advance / retreat phase index for one motor.
    void advancePhase(uint8_t idx);
    void retreatPhase(uint8_t idx);

    // Write the combined nibble pattern for all motors to the MCP23017.
    void applyPhases();

    // Build the 16-bit GPIOA/B word from per-motor nibbles.
    uint16_t buildGPIOAB() const;

    // Full-step nibble pattern per phase (forward sequence: 0→1→2→3).
    // Derived from the original hand-written constants:
    //   0xCCCC (phase 0), 0x6666 (phase 1), 0x3333 (phase 2), 0x9999 (phase 3)
    static const uint8_t PHASE_NIBBLE[STEP_PHASES];
};
