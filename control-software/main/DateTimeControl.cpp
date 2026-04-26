#include "DateTimeControl.h"

// ── Constructor ───────────────────────────────────────────────────────────────

DateTimeControl::DateTimeControl()
    : _h(12), _m(0), _s(0),
      _lastTickMs(0), _hoursChanged(false), _minutesChanged(false), _paused(false),
      _day(1), _month(1), _year(2025), _dateChanged(false),
      _alarmHour(7), _alarmMin(0), _alarmEnabled(false),
      _alarmArmed(true), _alarmFired(false)
{}

// ── Public ────────────────────────────────────────────────────────────────────

void DateTimeControl::begin() {
    _lastTickMs     = millis();
    _hoursChanged   = false;
    _minutesChanged = false;
    _dateChanged    = false;
    _alarmFired     = false;
    _paused         = false;
    _alarmArmed     = true;
}

void DateTimeControl::update() {
    if (_paused) return;
    unsigned long now = millis();
    while (now - _lastTickMs >= 1000UL) {
        _lastTickMs += 1000UL;
        tickSecond();
    }
}

void DateTimeControl::pauseTracking() {
    _paused = true;
}

void DateTimeControl::resumeTracking() {
    _paused     = false;
    _lastTickMs = millis();
}

// ── Change flags ──────────────────────────────────────────────────────────────

bool DateTimeControl::consumeHoursChanged() {
    if (_hoursChanged) { _hoursChanged = false; return true; }
    return false;
}

bool DateTimeControl::consumeMinutesChanged() {
    if (_minutesChanged) { _minutesChanged = false; return true; }
    return false;
}

bool DateTimeControl::consumeDateChanged() {
    if (_dateChanged) { _dateChanged = false; return true; }
    return false;
}

// ── Direct setters ────────────────────────────────────────────────────────────

void DateTimeControl::setTime(uint8_t h, uint8_t m) {
    _h = h % 24;
    _m = m % 60;
    _s = 0;
    _lastTickMs     = millis();
    _hoursChanged   = true;
    _minutesChanged = true;
}

void DateTimeControl::setDate(uint8_t day, uint8_t month, uint16_t year) {
    _month = (month >= 1 && month <= 12) ? month : 1;
    uint8_t maxDay = daysInMonth(_month, year);
    _day   = (day >= 1 && day <= maxDay) ? day : 1;
    _year  = year;
    _dateChanged = true;
}

// ── Increment helpers ─────────────────────────────────────────────────────────

void DateTimeControl::incrementHours() {
    _h = (_h + 1) % 24;
    _hoursChanged = true;
}

void DateTimeControl::incrementMinutes() {
    _m = (_m + 1) % 60;
    _minutesChanged = true;
}

void DateTimeControl::incrementDay() {
    uint8_t maxDay = daysInMonth(_month, _year);
    _day = (_day % maxDay) + 1;
    _dateChanged = true;
}

void DateTimeControl::incrementMonth() {
    _month = (_month % 12) + 1;
    uint8_t maxDay = daysInMonth(_month, _year);
    if (_day > maxDay) _day = maxDay;
    _dateChanged = true;
}

void DateTimeControl::incrementYear() {
    if (_year < 2099) _year++;
    // Clamp Feb 29 → Feb 28 when the new year is not a leap year
    if (_month == 2 && _day == 29 && !isLeapYear(_year)) _day = 28;
    _dateChanged = true;
}

// ── Alarm ─────────────────────────────────────────────────────────────────────

void DateTimeControl::toggleAlarmEnabled() {
    _alarmEnabled = !_alarmEnabled;
}

void DateTimeControl::setAlarmEnabled(bool en) {
    _alarmEnabled = en;
}

void DateTimeControl::incrementAlarmHour() {
    _alarmHour = (_alarmHour + 1) % 24;
}

void DateTimeControl::incrementAlarmMinute() {
    _alarmMin = (_alarmMin + 1) % 60;
}

void DateTimeControl::setAlarm(uint8_t h, uint8_t m) {
    _alarmHour = (h < 24) ? h : 0;
    _alarmMin  = (m < 60) ? m : 0;
}

bool DateTimeControl::consumeAlarmFired() {
    if (_alarmFired) { _alarmFired = false; return true; }
    return false;
}

void DateTimeControl::dismissAlarm() {
    _alarmArmed = false;
    _alarmFired = false;
}

// ── Private helpers ───────────────────────────────────────────────────────────

void DateTimeControl::tickSecond() {
    _s++;
    if (_s < 60) return;

    _s = 0;
    _m++;
    _minutesChanged = true;

    // Re-arm alarm once the clock has moved away from the alarm minute
    if (!_alarmArmed && (_h != _alarmHour || _m != _alarmMin)) {
        _alarmArmed = true;
    }

    checkAlarm();

    if (_m < 60) return;

    _m = 0;
    _h = (_h + 1) % 24;
    _hoursChanged = true;

    if (_h == 0) {
        // Midnight rollover → advance date
        uint8_t maxDay = daysInMonth(_month, _year);
        _day++;
        if (_day > maxDay) {
            _day = 1;
            _month++;
            if (_month > 12) {
                _month = 1;
                _year++;
            }
        }
        _dateChanged = true;
    }
}

void DateTimeControl::checkAlarm() {
    if (!_alarmEnabled || !_alarmArmed) return;
    if (_h == _alarmHour && _m == _alarmMin) {
        _alarmFired = true;
        _alarmArmed = false;
    }
}

uint8_t DateTimeControl::daysInMonth(uint8_t month, uint16_t year) {
    static const uint8_t days[12] = { 31, 28, 31, 30, 31, 30,
                                       31, 31, 30, 31, 30, 31 };
    if (month < 1 || month > 12) return 30;
    uint8_t d = days[month - 1];
    if (month == 2 && isLeapYear(year)) d = 29;
    return d;
}

bool DateTimeControl::isLeapYear(uint16_t year) {
    return (year % 4 == 0) && (year % 100 != 0 || year % 400 == 0);
}
