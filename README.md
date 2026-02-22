# 🎸 BLE MIDI Monitor

A lightweight Windows tool for monitoring **Bluetooth Low Energy (BLE)
MIDI** devices in real time.

This project was built as a practical debugging and exploration utility
for modern BLE MIDI instruments. It allows you to inspect raw MIDI
traffic, understand device behavior, and analyze message order with
precision.

------------------------------------------------------------------------

## 🚀 Features

-   Connects to **BLE MIDI** devices
-   Displays incoming MIDI messages in real time
-   Shows low-level MIDI events:
    -   Note On
    -   Note Off
    -   Control Change
    -   Program Change
    -   Pitch Bend
-   Precise timing visibility
-   Clean and minimal monitoring interface

------------------------------------------------------------------------

## 🎛 Tested With

-   AeroBand Guitar
-   Other BLE MIDI controllers and smart instruments

------------------------------------------------------------------------

## 🔎 Why This Tool Is Especially Useful

Some BLE MIDI instruments (for example, AeroBand Guitar) send **fret /
position data before the actual `Note On` message**.

With this monitor you can clearly see:

1.  The control or proprietary messages that indicate which fret is
    touched
2.  The exact moment when `Note On` is triggered
3.  The sequence of messages leading to sound generation

This is extremely useful when:

-   Developing MIDI-based software
-   Building synth logic
-   Debugging latency
-   Reverse engineering device behavior
-   Creating AI-assisted music tools
-   Understanding how smart guitars encode finger positions

You can literally observe which frets are touched *before* the note is
played.

------------------------------------------------------------------------

## 🛠 Use Cases

-   BLE MIDI debugging
-   Smart instrument analysis
-   MIDI message inspection
-   Reverse engineering proprietary MIDI behavior
-   Educational visualization of MIDI streams
-   Developing custom MIDI parsers or processors

------------------------------------------------------------------------

## 🎯 Target Audience

-   Developers working with BLE MIDI
-   Musicians experimenting with smart instruments
-   Engineers building custom MIDI software
-   Anyone curious about what their BLE MIDI device is actually sending

------------------------------------------------------------------------

## 💡 Motivation

Modern BLE instruments are powerful but often opaque in how they
transmit data.

This tool provides visibility and transparency, helping developers and
musicians understand exactly what happens under the hood.

If you work with BLE MIDI devices and need to **see precisely what is
being sent and in what order**, this project is for you.
