# Keylogger
Project Title: Keylogger with MongoDB Integration
Type: Individual Project
Duration: 01/2025 – 04/2025
Technology Stack: C#, .NET WinForms, MongoDB, Windows API, Task Scheduler

📝 Description:
This project is a full-featured keylogger application developed in C# using the WinForms framework, designed to monitor and record user keyboard activity in real-time. The system captures keystrokes, active window titles, and timestamps, then stores the data locally and remotely for later analysis.

The key features of the system include:

Keystroke Logging: Monitors and records every key pressed by the user with context-aware interpretation (Shift, Ctrl, function keys, etc.).

Active Window Tracking: Logs the title of the window in focus at the time of keystroke for contextual behavior analysis.

Daily Log Files: Automatically creates and appends to log files named by date (e.g., ddMMyyyy.txt).

MongoDB Integration: Logs are uploaded to a MongoDB database, enabling centralized storage and remote access to key activity data.

GUI with Filtering: A built-in graphical interface allows filtering logs by date and time range for quick review.

Run in Background: Includes a hidden mode and sets itself to auto-run at system startup using Windows Task Scheduler.

Thread Management: Logging runs in a background thread for performance and responsiveness.

File Hiding & Data Protection: Log files are set to hidden to protect from tampering or accidental deletion.

🎯 Objectives:
Practice real-time event monitoring on Windows systems

Learn to work with background services and OS-level integration (hooks, scheduler)

Combine GUI development with backend logging and database integration

Apply security knowledge in behavior tracking and data management

🧪 Tools & Technologies Used:
Languages: C# (.NET Framework)

Frontend: WinForms GUI

Database: MongoDB (local) via MongoDB.Driver

System APIs: user32.dll, kernel32.dll for keyboard hooks and window title detection

Windows Services: Task Scheduler automation using schtasks.exe

Threading: System.Threading.Thread for background logging

🔐 Security & Ethical Note:
This application was built solely for educational and ethical research purposes in the context of cybersecurity learning. It was not deployed in any uncontrolled environments and was only tested on virtual machines under the developer's control.

