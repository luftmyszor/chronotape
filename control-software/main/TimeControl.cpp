#include "TimeControl.h"

TimeControl::TimeControl()
    : _hours(0), _minutes(0), _seconds(0),
      _lastTickMs(0), _hoursChanged(false), _minutesChanged(false)
{}

void TimeControl::begin(uint8_t hours, uint8_t minutes) {
    _hours    = hours   % 24;
    _minutes  = minutes % 60;
    _seconds  = 0;
    _lastTickMs    = millis();
    _hoursChanged  = false;
    _minutesChanged = false;
}

void TimeControl::update() {
    unsigned long now = millis();
    // Advance one second each time 1000 ms have elapsed.
    // Using a loop handles the (rare) case where update() is called late.
    while (now - _lastTickMs >= 1000UL) {
        _lastTickMs += 1000UL;
        tickSecond();
    }
}

bool TimeControl::consumeHoursChanged() {
    if (_hoursChanged) {
        _hoursChanged = false;
        return true;
    }
    return false;
}

bool TimeControl::consumeMinutesChanged() {
    if (_minutesChanged) {
        _minutesChanged = false;
        return true;
    }
    return false;
}

void TimeControl::setTime(uint8_t hours, uint8_t minutes) {
    _hours    = hours   % 24;
    _minutes  = minutes % 60;
    _seconds  = 0;
    _lastTickMs    = millis();
    _hoursChanged  = true;
    _minutesChanged = true;
}

void TimeControl::incrementHours() {
    _hours = (_hours + 1) % 24;
    _hoursChanged = true;
}

void TimeControl::incrementMinutes() {
    _minutes = (_minutes + 1) % 60;
    _minutesChanged = true;
}

// ── Private ───────────────────────────────────────────────────────────────────

void TimeControl::tickSecond() {
    _seconds++;
    if (_seconds < 60) return;

    _seconds = 0;
    _minutes++;
    _minutesChanged = true;

    if (_minutes < 60) return;

    _minutes = 0;
    _hours   = (_hours + 1) % 24;
    _hoursChanged = true;
}
