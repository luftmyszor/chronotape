#pragma once
#include <Arduino.h>

// ─────────────────────────────────────────────────────────────────────────────
// TimeControl
//
// Tracks the current time (HH:MM:SS) using millis().  No RTC chip is required.
// Callers poll consumeHoursChanged() / consumeMinutesChanged() to find out
// when a display update is needed; each flag is cleared on the first read.
//
// update() must be called every loop iteration.
// ─────────────────────────────────────────────────────────────────────────────
class TimeControl {
public:
    TimeControl();

    // Set the starting time and begin tracking.
    void begin(uint8_t hours = 0, uint8_t minutes = 0);

    // Advance internal counters based on elapsed millis.
    void update();

    uint8_t getHours()   const { return _hours;   }
    uint8_t getMinutes() const { return _minutes; }
    uint8_t getSeconds() const { return _seconds; }

    // Returns true once after hours have changed; clears the flag.
    bool consumeHoursChanged();
    // Returns true once after minutes have changed; clears the flag.
    bool consumeMinutesChanged();

    // Directly set the time (e.g. from the Set-Time mode).
    void setTime(uint8_t hours, uint8_t minutes);

    // Increment helpers for interactive time-setting.
    void incrementHours();
    void incrementMinutes();

private:
    uint8_t       _hours;
    uint8_t       _minutes;
    uint8_t       _seconds;
    unsigned long _lastTickMs;  // millis() at the last 1-second tick
    bool          _hoursChanged;
    bool          _minutesChanged;

    void tickSecond();
};
