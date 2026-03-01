# 🖥 MotorPanel

## PC-Based Visualization & Control Interface  
**Modular BLDC Motor Test Bench – Diploma Thesis**  
HTL Anichstraße  
Department of Electrical Engineering & Mechatronics  
Class 5AHET (2025/26)

---

## 📂 Repository Scope

This repository contains the **Windows-based graphical user interface** for communication between the motor control microcontroller and the PC.

MotorPanel serves as the visualization, configuration, and diagnostic layer of the complete motor test bench system.

It includes:

- 🔌 UART (Serial) communication interface  
- 📊 Real-time data visualization  
- 🎛 Motor parameter configuration  
- 🔎 System diagnostics & status monitoring  
- 📈 Live current, voltage, torque, and temperature plotting  

---

## ⚙️ System Architecture

MotorPanel is part of the overall modular test bench system:

### Communication Concept

- Serial communication at **115200 baud**
- Packet-based protocol with start byte synchronization (0xAA)
- Binary data transmission with scaled integer values
- Status flags for errors and warnings
- Deterministic real-time data exchange

---

## 📊 Real-Time Monitoring

The application visualizes key motor parameters:

### Electrical Parameters
- Id / Iq currents  
- Vd / Vq voltages  
- DC-Link current (I_Bus)  
- DC-Link voltage (V_Bus)  

### Mechanical Parameters
- Motor speed  
- Torque  

### Thermal Monitoring
- PCB temperature  
- Winding temperature  

Data is displayed via:

- Circular gauges  
- Linear temperature indicators  
- Real-time line charts with auto-scrolling  

---

## 🎛 Control Functions

MotorPanel allows direct interaction with the drive system:

### Control Modes
- Speed control  
- Torque (Iq) control  

### Control Strategies
- Block commutation  
- Field-Oriented Control (FOC)  
- Sensored operation  
- Sensorless operation  

### Actuators
- Motor enable / disable  
- Start / stop  
- Rotation direction  
- Target speed / torque configuration  

All commands are transmitted as structured control packets to the microcontroller.

---

## 🛠 Technical Implementation

- Language: C#  
- Framework: WPF (.NET)  
- UI Components: Syncfusion WPF Gauges & Charts  
- Communication: `System.IO.Ports.SerialPort`  
- Data Handling: ObservableCollection with real-time binding  

The UI is fully custom-styled with reusable toggle components and animated controls.

---

## 📦 Project Context

MotorPanel is part of the complete system:

- ⚡ PowerBoard v1 – Hardware Revision (Archived)
- ⚡ PowerBoard v2 – Final Hardware Revision
- 🖥 MotorPanel – PC Visualization & Control
- 💻 Embedded Firmware – Microcontroller Control Logic

---

## 🎓 Educational Purpose

This application enables students to:

- Analyze real-time motor behavior  
- Observe FOC current regulation  
- Understand drive system dynamics  
- Investigate control algorithm behavior  
- Study system-level interaction between software & power electronics  

It serves as the graphical interface between theory and practical experimentation.

---
