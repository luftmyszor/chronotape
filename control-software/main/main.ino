#include <Wire.h>
#include <Adafruit_MCP23X17.h>

Adafruit_MCP23X17 mcp;

#define STEP_DELAY 2

void setup() {
  Serial.begin(9600);
  
  // Initialize the MCP23017 on the default I2C address (0x20)
  if (!mcp.begin_I2C()) {
    Serial.println("Error: MCP23017 not found. Check wiring.");
    while (1); // Halt if not found
  }

  // Set all 16 pins (0-15) as OUTPUTs
  for (int i = 0; i < 16; i++) {
    mcp.pinMode(i, OUTPUT);
  }
}

void loop() {
  // STEP 1: Turn on IN1 & IN2 for all four motors
  // Binary: 1100 1100 1100 1100 (Hex: 0xCCCC)
  mcp.writeGPIOAB(0xCCCC);
  delay(STEP_DELAY);

  // STEP 2: Turn on IN2 & IN3 for all four motors
  // Binary: 0110 0110 0110 0110 (Hex: 0x6666)
  mcp.writeGPIOAB(0x6666);
  delay(STEP_DELAY);

  // STEP 3: Turn on IN3 & IN4 for all four motors
  // Binary: 0011 0011 0011 0011 (Hex: 0x3333)
  mcp.writeGPIOAB(0x3333);
  delay(STEP_DELAY);

  // STEP 4: Turn on IN4 & IN1 for all four motors
  // Binary: 1001 1001 1001 1001 (Hex: 0x9999)
  mcp.writeGPIOAB(0x9999);
  delay(STEP_DELAY);
}