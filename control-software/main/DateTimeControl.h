// ─────────────────────────────────────────────────────────────────────────────
// DateTimeControl.h
//
// Tracks the current Time (H:M:S) and Date (D/M/Y) using millis().
// Also manages an Alarm (hour, minute, enabled) with automatic arm/disarm logic.
//
// No RTC chip required.  update() must be called every loop iteration.
//
// Time tracking can be paused (e.g. during TAPE_ADJUST mode) and resumed
// without losing the accumulated time value; millis() drift during the pause is
// discarded so the clock does not jump on resume.
//
// All data is strictly volatile — state resets to defaults on power loss.
// ─────────────────────────────────────────────────────────────────────────────
#pragma once
#include <Arduino.h>

class DateTimeControl {
public:
    DateTimeControl();

    // Initialise from defaults and start tracking from the current millis().
    void begin();

    // Advance internal counters based on elapsed millis(); no-op while paused.
    // Call every loop iteration.
    void update();

    // Pause / resume millis-based time tracking (use during SYNC mode).
    // resumeTracking() resets the millis reference so no time is skipped.
    void pauseTracking();
    void resumeTracking();

    // ── Time getters ──────────────────────────────────────────────────────────
    uint8_t getHours()   const { return _h; }
    uint8_t getMinutes() const { return _m; }
    uint8_t getSeconds() const { return _s; }

    // ── Date getters ──────────────────────────────────────────────────────────
    uint8_t  getDay()   const { return _day;   }
    uint8_t  getMonth() const { return _month; }
    uint16_t getYear()  const { return _year;  }

    // ── Change flags ──────────────────────────────────────────────────────────
    // Each returns true exactly once after the corresponding value changes,
    // then clears automatically.
    bool consumeHoursChanged();
    bool consumeMinutesChanged();
    bool consumeDateChanged();

    // ── Direct setters (from SET_TIME / SET_DATE modes) ───────────────────────
    void setTime(uint8_t h, uint8_t m);
    void setDate(uint8_t day, uint8_t month, uint16_t year);

    // ── Increment helpers ─────────────────────────────────────────────────────
    void incrementHours();
    void incrementMinutes();
    void incrementDay();
    void incrementMonth();
    void incrementYear();

    // ── Alarm ─────────────────────────────────────────────────────────────────
    uint8_t getAlarmHour()   const { return _alarmHour; }
    uint8_t getAlarmMinute() const { return _alarmMin;  }
    bool    isAlarmEnabled() const { return _alarmEnabled; }

    void toggleAlarmEnabled();
    void setAlarmEnabled(bool en);
    void setAlarm(uint8_t h, uint8_t m);   // Set alarm time directly
    void incrementAlarmHour();
    void incrementAlarmMinute();

    // Returns true exactly once when the alarm fires; clears the flag.
    bool consumeAlarmFired();

    // Call when the user dismisses the ringing alarm.
    // The alarm re-arms automatically once the clock leaves the alarm minute.
    void dismissAlarm();

private:
    // Time
    uint8_t       _h, _m, _s;
    unsigned long _lastTickMs;
    bool          _hoursChanged;
    bool          _minutesChanged;
    bool          _paused;

    // Date
    uint8_t  _day, _month;
    uint16_t _year;
    bool     _dateChanged;

    // Alarm
    uint8_t _alarmHour, _alarmMin;
    bool    _alarmEnabled;
    bool    _alarmArmed;   // False after firing; re-arms once the minute changes
    bool    _alarmFired;   // Pending event for consumeAlarmFired()

    // Internal tick helpers
    void tickSecond();
    void checkAlarm();

    static uint8_t daysInMonth(uint8_t month, uint16_t year);
    static bool    isLeapYear(uint16_t year);
};
