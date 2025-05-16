using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;

namespace UdpPlayer
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint([In] Point pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        // 작업 표시줄을 무시하기 위한 상수 선언
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_FRAMECHANGED = 0x0020;
        private const int SWP_SHOWWINDOW = 0x0040;

        // Monitor DPI 유형
        public enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2,
            MDT_DEFAULT = MDT_EFFECTIVE_DPI
        }

        private string[] m_hook_check_array = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "#", "h", "Up", "Down", "Left", "Right", "VolumeUp", "VolumeDown", "ControlButton" };

        public Dictionary<string, Dictionary<string, string>> m_config_ini_data { get; private set; }
        public Dictionary<string, Dictionary<string, string>> m_setting_ini_data { get; private set; }
        private readonly string m_config_file_path = "config.ini";
        private readonly string m_setting_file_path = "setting.ini";

        private WebView2 m_webview_main = null;
        private int m_webserver_port = 80;
        private double m_webViewZoom = 1.0;
        private Boolean m_webServer = true;
        private double m_display_scale = 1.0;
        private int m_udp_port = 81;
        private string m_udp_mode = "server";

        private int m_screen_width = 1280;
        private int m_screen_height = 720;
        private int m_player_left = 0;
        private int m_player_top = 0;
        private int m_player_width = 1280;
        private int m_player_height = 720;
        private bool m_fullscreen = false;
        private bool m_topMost = false;
        private string m_cusor = "show";

        private FileStream m_log_file_stream = null;
        private StreamWriter m_log_stream_write = null;
        private string m_log_file_path = "";
        private string m_log_file_name = "";

        private string m_main_webview_url = "";
        private string m_target_url = "";
        private bool m_mouse_show = true;

        private System.Threading.Timer m_mostTopTimer;
        private System.Threading.Timer m_control_receiveTimer;
        private System.Threading.Timer m_sensor_receiveTimer;
        private System.Threading.Timer m_serialTimer;

        static UdpClient m_main_udp_client;

        private string m_control_com_port = "";
        private int m_control_com_rate;
        private string m_sensor_com_port = "";
        private int m_sensor_com_rate;
        private StringBuilder receivedControlData = new StringBuilder();
        private StringBuilder receivedSensorTempData = new StringBuilder();
        private string receivedSensorData = "";
        static int consecutive59Count = 0; // `59` 연속 카운트
        static bool isAppending = false; // 현재 데이터를 추가 중인지 여부

        private int m_serial_timer_chk = 0;
        private bool m_serial_timer_bool = false;

        private int m_sensor_dist = 9999;
        private int m_sensor_active_cnt = 0;
        private int m_sensor_inactive_cnt = 0;
        private int m_sensor_max = 10;

        private Boolean m_khook_mode = false;
        private Boolean m_button_state = false;
        private Boolean m_sensor_is_first = true;
        private Boolean m_sensor_active = false;

        private bool m_webview_main_loaded = false;


        private HookManager hookManager = new HookManager();
        public static Form1 m_form { get; private set; }

        public Form1()
        {
            try
            {
                //윈도우 디스플레이 배율을 확인하기 위한 함수
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

                m_form = this;


                //로그 작성을 위한 함수
                CreateLogFile();

                SetLog("[------------------------------------------]");
                SetLog("[" + Process.GetCurrentProcess().ProcessName + "] Start");

                InitializeComponent();

                SetLoadSettingIni();
                GetDisplayScale();



                //프로세스 종료시 웹서버가 혹시 켜져있으면 죽이기 위한 함수
                string processName = "SysWebServer"; // 대상 프로세스의 이름
                AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
                {
                    hookManager.SetUnHook();
                    // 대상 프로세스 종료
                    Process[] processes = Process.GetProcessesByName(processName);
                    foreach (Process process in processes)
                    {
                        SetLog("[WebServer] Kill");
                        process.Kill();
                    }
                    SetLog("[" + Process.GetCurrentProcess().ProcessName + "] Stop");
                    SetLog("[------------------------------------------]");
                };


            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }

        private void InitializeUdpClient()
        {
            if (m_udp_mode == "server")
            {
                m_main_udp_client = new UdpClient();
                m_main_udp_client.EnableBroadcast = true;
            }
            else if (m_udp_mode == "client")
            {
                m_main_udp_client = new UdpClient(m_udp_port);

                Thread receiveThread = new Thread(SetReceiveLoop);
                receiveThread.IsBackground = true; // 백그라운드로 실행
                receiveThread.Start();
            }
        }

        private void SetReceiveLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    byte[] receiveBytes = m_main_udp_client.Receive(ref remoteEP);
                    string receiveData = Encoding.UTF8.GetString(receiveBytes);
                    Console.WriteLine($"수신: \"{receiveData}\" ← {remoteEP.Address}:{remoteEP.Port}");
                    setCallAppToWeb("UDP_RECV|"+ receiveData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"수신 오류: {ex.Message}");
                    SetCallAppToWeb("UDP_RECV|ERROR^" + ex.Message);
                    break;
                }
            }
        }
        private void SendBroadcastMessage(string message)
        {
            byte[] sendBytes = Encoding.UTF8.GetBytes(message);

            IPAddress broadcastIp = IPAddress.Parse("255.255.255.255");
            int targetPort = m_udp_port;

            IPEndPoint broadcastEP = new IPEndPoint(broadcastIp, targetPort);

            try
            {
                m_main_udp_client.Send(sendBytes, sendBytes.Length, broadcastEP);
                Console.WriteLine($"브로드캐스트 보냄: \"{message}\" → {broadcastEP}");
                //SetCallAppToWeb("RECV|" + message + "^" + broadcastEP);
                SetLog("[UDP SEND] " + message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}");
                SetCallAppToWeb("UDP_SEND|ERROR^" + ex.Message);
            }
        }

        //웹뷰에 값을 전달하기 위한 함수
        private void SetCallAppToWeb(string _str)
        {
            m_webview_main.BeginInvoke(new Action(() =>
            {
                m_webview_main.CoreWebView2.PostWebMessageAsString(_str);
            }));
        }

        //ini파일을 읽어오기 위한 함수
        private void SetLoadSettingIni()
        {
            try
            {
                m_setting_ini_data = ReadIniFile(m_setting_file_path);
                if (m_setting_ini_data != null)
                {
                    SetLog("[Setting File] Readed");
                    try
                    {
                        m_webServer = Convert.ToBoolean(m_setting_ini_data["server"]["webServer"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] webServer 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_webserver_port = Convert.ToInt32(m_setting_ini_data["server"]["port"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] server port 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_target_url = Convert.ToString(m_setting_ini_data["server"]["targetUrl"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] targetUrl 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_khook_mode = Convert.ToBoolean(m_setting_ini_data["player"]["kHook"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] kHook 없음 > " + exception.Message);
                    }
                    if (m_khook_mode == true)
                    {
                        hookManager.SetHook();
                    }
                    try
                    {
                        m_udp_mode = Convert.ToString(m_setting_ini_data["udp"]["mode"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] udp mode 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_udp_port = Convert.ToInt32(m_setting_ini_data["udp"]["port"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] udp port 없음 > " + exception.Message);
                    }

                    try
                    {
                        m_control_com_port = Convert.ToString(m_setting_ini_data["controlBoard"]["port"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] controlBoard port 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_sensor_com_port = Convert.ToString(m_setting_ini_data["sensor"]["port"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] sensor port 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_sensor_dist = Convert.ToInt32(m_setting_ini_data["sensor"]["dist"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] sensor dist 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_sensor_max = Convert.ToInt32(m_setting_ini_data["sensor"]["max"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] sensor max 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_sensor_com_rate = Convert.ToInt32(m_setting_ini_data["sensor"]["rate"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Setting] sensor rate 없음 > " + exception.Message);
                    }
                    if (m_setting_ini_data["controlBoard"]["rate"] == "")
                    {
                        m_control_com_rate = 0;
                    }
                    else
                    {
                        try
                        {
                            m_control_com_rate = Convert.ToInt32(m_setting_ini_data["controlBoard"]["rate"]);
                        }
                        catch (Exception exception)
                        {
                            SetLog("[Setting] controlBoard rate 없음 > " + exception.Message);
                        }
                    }
                    _ = InitializeControlComport();
                    _ = InitializeSensorComport();
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }

            InitializeUdpClient();

            SetLoadConfigIni();
        }

        private async Task InitializeControlComport()
        {
            try
            {
                if (serialPort_control.IsOpen)
                {
                    serialPort_control.Close();
                    Console.WriteLine("CLOSE");
                }

                if (!serialPort_control.IsOpen)
                {
                    serialPort_control.PortName = m_control_com_port;
                    serialPort_control.BaudRate = m_control_com_rate;
                    serialPort_control.DataBits = 8;
                    serialPort_control.StopBits = StopBits.One;
                    serialPort_control.Parity = Parity.None;
                    serialPort_control.DataReceived += new SerialDataReceivedEventHandler(serialPort_control_DataReceived);

                    serialPort_control.ReadTimeout = 1000; // 1초 타임아웃 설정 예시
                    serialPort_control.WriteTimeout = 1000; // 1초 타임아웃 설정 예시

                    try
                    {
                        var openTask = Task.Run(() =>
                        {
                            try
                            {
                                serialPort_control.Open();
                            }
                            catch (IOException exception)
                            {
                                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception.Message);
                                // 비동기 작업 내부에서 발생한 예외 처리
                                //throw new IOException("Failed to open control serial port.", exception.Message);
                            }
                        });

                        if (await Task.WhenAny(openTask, Task.Delay(5000)) == openTask)
                        {
                            // 시리얼 포트를 성공적으로 열었을 때
                            SetLog("[Control Com Port] " + m_control_com_port + " Port is Opened");
                            Console.WriteLine("포트 오픈!");
                        }
                        else
                        {
                            // 타임아웃 발생 시
                            SetLog("[Control Com Port] [Error] " + "[" + m_control_com_port + "] " + "Opening port timed out.");
                            Console.WriteLine("포트 열기 타임아웃.");
                        }
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Control Com Port] [Error] " + "[" + m_control_com_port + "] " + exception.ToString());
                        Console.WriteLine(exception.ToString());
                    }
                }
                else
                {
                    SetLog("[Control Com Port] [Error] " + "[" + m_control_com_port + "] " + "Port is Busy");
                    Console.WriteLine("포트가 이미 열려 있습니다.");
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception.Message);
            }
        }

        //컴포트로부터 값을 넘겨받기 위한 함수
        private void serialPort_control_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // 타이머가 이미 동작 중이면 중지합니다.
            m_control_receiveTimer?.Dispose();
            // 새 타이머를 생성하여 수신을 기다립니다.
            m_control_receiveTimer = new System.Threading.Timer(Process_control_receivedData, null, 50, Timeout.Infinite);

            while (serialPort_control.BytesToRead > 0)
            {
                receivedControlData.AppendFormat("{0:X2} ", (byte)serialPort_control.ReadByte());
            }
        }
        private void Process_control_receivedData(object state)
        {
            try
            {
                if (receivedControlData.Length > 0)
                {
                    string recvData = receivedControlData.ToString().Trim();
                    Console.WriteLine("RECV Control] " + recvData);
                    receivedControlData.Clear(); // 버퍼를 비웁니다.
                    //23 40 30 31 21 21
                    string[] t_list = recvData.Split(' ');

                    //Console.WriteLine("m_headset_state : " + m_headset_state);
                    if (t_list[2] == "30")
                    {
                        //이어폰 빠짐
                    }
                    else if (t_list[2] == "31")
                    {
                        //이어폰 꽂힘
                    }

                    if (t_list[3] == "30")
                    {
                        //버튼 뗌
                    }
                    else if (t_list[3] == "31")
                    {
                        setCallAppToWeb("BUTTON|ON");
                        //버튼 눌림
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        private async Task InitializeSensorComport()
        {
            try
            {
                if (serialPort_sensor.IsOpen)
                {
                    serialPort_sensor.Close();
                    Console.WriteLine("CLOSE");
                }

                if (!serialPort_sensor.IsOpen)
                {
                    serialPort_sensor.PortName = m_sensor_com_port;
                    serialPort_sensor.BaudRate = m_sensor_com_rate;
                    serialPort_sensor.DataBits = 8;
                    serialPort_sensor.StopBits = StopBits.One;
                    serialPort_sensor.Parity = Parity.None;
                    serialPort_sensor.DataReceived += new SerialDataReceivedEventHandler(serialPort_sensor_DataReceived);

                    serialPort_sensor.ReadTimeout = 1000; // 1초 타임아웃 설정 예시
                    serialPort_sensor.WriteTimeout = 1000; // 1초 타임아웃 설정 예시

                    // 타이머가 이미 동작 중이면 중지합니다.
                    m_sensor_receiveTimer?.Dispose();
                    // 새 타이머를 생성하여 수신을 기다립니다.
                    m_sensor_receiveTimer = new System.Threading.Timer(Process_sensor_receivedData, null, 0, 100);

                    try
                    {
                        var openTask = Task.Run(() =>
                        {
                            try
                            {
                                serialPort_sensor.Open();
                            }
                            catch (IOException exception)
                            {
                                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception.Message);
                                // 비동기 작업 내부에서 발생한 예외 처리
                                //throw new IOException("Failed to open sensor serial port.", exception.Message);
                            }
                        });

                        if (await Task.WhenAny(openTask, Task.Delay(5000)) == openTask)
                        {
                            // 시리얼 포트를 성공적으로 열었을 때
                            SetLog("[Sensor Com Port] " + m_sensor_com_port + " Port is Opened");
                            Console.WriteLine("포트 오픈!");
                        }
                        else
                        {
                            // 타임아웃 발생 시
                            SetLog("[Sensor Com Port] [Error] " + "[" + m_sensor_com_port + "] " + "Opening port timed out.");
                            Console.WriteLine("포트 열기 타임아웃.");
                        }
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Sensor Com Port] [Error] " + "[" + m_sensor_com_port + "] " + exception.ToString());
                        Console.WriteLine(exception.ToString());
                    }
                }
                else
                {
                    SetLog("[Sensor Com Port] [Error] " + "[" + m_sensor_com_port + "] " + "Port is Busy");
                    Console.WriteLine("포트가 이미 열려 있습니다.");
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception.Message);
            }
        }
        private void serialPort_sensor_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            while (serialPort_sensor.BytesToRead > 0)
            {
                // 한 바이트씩 읽고 16진수 형식으로 변환
                string hexData = serialPort_sensor.ReadByte().ToString("X2");

                if (hexData == "59")
                {
                    consecutive59Count++;

                    // 첫 번째 `5959` 감지로 데이터 수신 시작
                    if (consecutive59Count == 2 && !isAppending)
                    {
                        isAppending = true;
                        receivedSensorTempData.Clear(); // 데이터를 새로 수신하기 시작
                        receivedSensorTempData.Append("5959"); // 첫 `5959` 추가
                    }
                    // 이미 데이터를 추가 중일 때, 새 `5959`가 감지되면 기존 데이터 초기화 후 새로 추가
                    else if (consecutive59Count == 2 && isAppending)
                    {
                        receivedSensorTempData.Clear();
                        receivedSensorTempData.Append("5959");
                    }
                }
                else
                {
                    // `59` 연속이 끊겼을 때 처리
                    consecutive59Count = 0;

                    // `5959` 패턴을 수신 중인 경우만 데이터 추가
                    if (isAppending)
                    {
                        receivedSensorTempData.Append(hexData);
                    }
                }
                if (receivedSensorTempData.Length == 18)
                {
                    //Console.WriteLine(receivedSensorTempData.Length);
                    receivedSensorData = receivedSensorTempData.ToString();
                    //Console.WriteLine("현재 데이터 (16진수): " + receivedSensorData.ToString());
                }
            }
            //Console.WriteLine("1> "+receivedSensorTempData.ToString().Trim());
        }
        private void Process_sensor_receivedData(object state)
        {
            try
            {
                if (convSensorData(receivedSensorData) == "")
                {
                    return;
                }
                int t_dist = Convert.ToInt32(convSensorData(receivedSensorData));
                SetSensorControl(t_dist);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
        private string convSensorData(string _str)
        {
            string t_str = "";
            if (_str.Length == 18 && _str.Substring(0, 4) == "5959")
            {
                string t_dist_low = _str.Substring(4, 2);
                string t_dist_high = _str.Substring(6, 2);
                string t_strength_low = _str.Substring(6, 2);
                string t_strength_high = _str.Substring(10, 2);
                string t_temp_low = _str.Substring(12, 2);
                string t_temp_high = _str.Substring(14, 2);

                int t_low = Convert.ToInt32(t_dist_low, 16);
                int t_high = Convert.ToInt32(t_dist_high, 16);
                int t_s_low = Convert.ToInt32(t_strength_low, 16);
                int t_s_high = Convert.ToInt32(t_strength_high, 16);

                //t_str = "LOW : " + t_low.ToString() + ", HIGH : " + t_high.ToString() + ", S_LOW : " + t_s_low.ToString() + ", S_HIGH : " + t_s_high.ToString();
                t_str = t_low.ToString();
            }

            return t_str;
        }

        private void SetSensorControl(int _dist)
        {
            //Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff"));
            if (_dist > m_sensor_dist)
            {
                if (m_sensor_active == true)
                {
                    m_sensor_active_cnt = 0;
                    //0.1초에 한번씩 들어옴
                    m_sensor_inactive_cnt += 1;
                    if (m_sensor_inactive_cnt > m_sensor_max)
                    {
                        m_sensor_inactive_cnt = 0;
                        //최신 상태가 센서 감지 일 때
                        //setCallAppToWeb("SENSOR|OFF");
                        m_sensor_active = false;
                    }
                }
                //센서 미감지
                else
                {
                    if (m_sensor_is_first == true)
                    {
                        m_sensor_is_first = false;
                        m_sensor_active = false;
                    }
                }
            }
            else
            {
                if (m_sensor_active == false)
                {
                    m_sensor_inactive_cnt = 0;
                    //0.1초에 한번씩 들어옴
                    m_sensor_active_cnt += 1;
                    if (m_sensor_active_cnt > m_sensor_max / 2)
                    {
                        m_sensor_active_cnt = 0;
                        //최신 상태가 센서 미감지 일 때
                        //setCallAppToWeb("SENSOR|ON");
                        m_sensor_active = true;
                        //setCallAppToWeb("SENSOR|DIST|" + _dist.ToString());
                        //센서 감지
                    }
                }
                else
                {
                }
            }
        }


        //ini파일을 읽어오기 위한 함수
        private void SetLoadConfigIni()
        {
            try
            {
                Rectangle cursorScreenBounds = Screen.FromPoint(Cursor.Position).Bounds;
                m_screen_width = cursorScreenBounds.Width;
                m_screen_height = cursorScreenBounds.Height;
                m_config_ini_data = ReadIniFile(m_config_file_path);

                if (m_config_ini_data != null)
                {

                    SetLog("[Config File] Readed");
                    try
                    {
                        m_player_left = Convert.ToInt32(m_config_ini_data["player"]["left"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Config] left 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_player_top = Convert.ToInt32(m_config_ini_data["player"]["top"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Config] top 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_player_width = Convert.ToInt32(m_config_ini_data["player"]["width"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Config] width 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_player_height = Convert.ToInt32(m_config_ini_data["player"]["height"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Config] height 없음 > " + exception.Message);
                    }

                    try
                    {
                        m_fullscreen = Convert.ToBoolean(m_config_ini_data["player"]["fullscreen"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Config] fullscreen 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_topMost = Convert.ToBoolean(m_config_ini_data["player"]["alwaysTop"]);
                    }
                    catch (Exception exception2)
                    {
                        SetLog("[Config] alwaysTop 없음 > " + exception2.Message);
                    }
                    try
                    {
                        m_cusor = Convert.ToString(m_config_ini_data["player"]["mouse"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Config] mouse 없음 > " + exception.Message);
                    }
                    try
                    {
                        m_webViewZoom = Convert.ToDouble(m_config_ini_data["player"]["webViewZoom"]);
                    }
                    catch (Exception exception)
                    {
                        SetLog("[Config] webViewZoom 없음 > " + exception.Message);
                    }

                    if (m_player_width == 0 && m_player_height == 0)
                    {
                        m_player_width = (int)Math.Round(m_screen_width / m_display_scale);
                    }
                    if (m_player_height == 0)
                    {
                        m_player_height = (int)Math.Round(m_screen_height / m_display_scale);
                    }

                    //마우스 커서가 hide이면 마우스 커서 숨김
                    if (m_cusor == "hide")
                    {
                        m_mouse_show = true;
                        SetHideCursor();
                    }

                    //웹서버를 사용하기로 해 놓으면
                    if (m_webServer == true)
                    {
                        InitializeWebServer();
                        Task task = InitializeWebView();
                    }
                    else
                    {
                        Task task = InitializeWebView();
                    }

                    //프로그램의 크기 조정
                    this.Size = new System.Drawing.Size(m_player_width, m_player_height);

                    //전체화면으로 보기 위한 함수
                    if (m_fullscreen == true)
                    {
                        this.FormBorderStyle = FormBorderStyle.None;
                        IntPtr hWnd = this.Handle;
                        SetWindowPos(hWnd, IntPtr.Zero, m_player_left, m_player_top, m_player_width, m_player_height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                    }
                    else
                    {
                        this.Left = m_player_left;
                        this.Top = m_player_top;
                    }

                    //항상 최상위일때 최상위 모드
                    if (m_topMost == true)
                    {
                        this.TopMost = true;
                        m_mostTopTimer = new System.Threading.Timer(OnCheckMostTopEvent, null, 5000, 10000);
                    }
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }


        //웹서버 구동을 위한 함수
        private void InitializeWebServer()
        {
            try
            {
                string programPath = "SysWebServer.exe";
                SetLog("[WebServer] [Init] " + programPath);
                // 파일이 존재하는지 확인
                if (File.Exists(programPath))
                {
                    // 프로그램이 이미 실행 중인지 확인
                    Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(programPath));
                    if (processes.Length > 0)
                    {
                        // 이미 실행 중이면 강제 종료
                        foreach (Process process in processes)
                        {
                            try
                            {
                                process.Kill();
                                process.WaitForExit(); // 종료될 때까지 대기
                            }
                            catch (Exception exception)
                            {
                                SetLog("[WebServer Error] " + exception.Message);
                                Console.WriteLine($"프로세스 에러: {exception.Message}");
                            }
                        }
                    }

                    // 프로그램 실행
                    SetLog("[WebServer] [Start]");
                    Process.Start(programPath);
                }
                else
                {
                    SetLog("[WebServer] [Error] File Not Found");
                    Console.WriteLine("파일이 존재하지 않습니다.");
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }

        //웹뷰 초기화
        private async Task InitializeWebView()
        {
            try
            {
                m_webview_main = new WebView2
                {
                    Dock = DockStyle.Fill,
                    DefaultBackgroundColor = Color.Black,
                    Visible = false
                };
                Controls.Add(m_webview_main);
                SetLog("[Webview] Add to Control Complete");
                try
                {
                    //동영상/오디오 자동 재생시 소리가 재생되게 하기 위한 코드
                    var environment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
                    await m_webview_main.EnsureCoreWebView2Async(environment);
                    SetLog("[Webview] WebView2 초기화 완료");
                }
                catch (Exception exception)
                {
                    SetLog("[Webview] WebView2 초기화 실패 : " + exception.Message);
                }
                m_webview_main.WebMessageReceived += (s, e) =>
                {
                    SetCallWebToApp(e.WebMessageAsJson);
                };

                //브라우저 캐시 지우기 함수
                ClearBrowserCache();
                Thread.Sleep(100);

                if (m_webServer == true)
                {
                    //웹서버 사용이 true 일때
                    if (m_target_url == "")
                    {
                        m_main_webview_url = "http://localhost:" + m_webserver_port;
                        //타겟 url이 없으면 로컬호스트:port를 웹뷰에 띄운다
                    }
                    else
                    {
                        m_main_webview_url = m_target_url;
                        //타겟 url이 있으면 타켓 url을 웹뷰에 띄운다
                    }
                }
                else
                {
                    //웹서버 사용이 false 일때
                    if (m_target_url == "")
                    {
                        m_main_webview_url = "http://localhost";
                        //타켓 url이 없으면 로컬호스트를 웹뷰에 띄운다
                    }
                    else
                    {
                        m_main_webview_url = m_target_url;
                    }
                }

                Console.WriteLine(m_main_webview_url);
                m_webview_main.CoreWebView2.Navigate(m_main_webview_url);
                SetLog("[Webview] load to " + m_main_webview_url);
                //웹뷰의 모든 쿠키 삭제
                m_webview_main.CoreWebView2.CookieManager.DeleteAllCookies();
                Thread.Sleep(500);

                m_webview_main.NavigationCompleted += WebView2_NavigationCompleted;
                //웹뷰 우클릭 막기
                m_webview_main.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                //웹뷰 다이얼로그 팝업 막기
                m_webview_main.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                //웹뷰를 지정된 scale로 확대/축소
                m_webview_main.ZoomFactor = m_webViewZoom / m_display_scale;
                SetLog("[Webview] Add Option Complete");
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }

        private void WebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                m_webview_main.Visible = true;
                SetLog("[Webview] set zoom " + (m_webViewZoom / m_display_scale));
                m_webview_main_loaded = true;
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }

        //브라우저 캐시 삭제 소스
        private async void ClearBrowserCache()
        {
            await m_webview_main.CoreWebView2.Profile.ClearBrowsingDataAsync();
            SetLog($"[WebView] Clear Cache");
        }

        //html에서 값을 전송받기위한 함수
        private void SetCallWebToApp(string _str)
        {
            try
            {
                //STATUS ${STATUS} 이런 식으로 값이 전달됨
                Console.WriteLine("SetCallWebToApp>>" + _str);
                string pattern = @"(?<key>\w+)\s+\$\{(?<value>[^\}]+)\}";
                Match match = Regex.Match(_str, pattern);
                //전송받은 값을 분리하기 위한 함수
                if (match.Success)
                {
                    string key = match.Groups["key"].Value;
                    string value = match.Groups["value"].Value;
                    string[] array = value.Split('|');
                    SetLog("[WebToApp] " + key + ", " + value);
                    if (key == "STATUS")
                    {
                        if (array[0] == "STATUS")
                        {
                            Console.WriteLine("상태체크");
                            //m_status_sec = DateTime.Now;
                        }
                    }
                    else if (key == "SetLog")
                    {
                        //SetLog(array[0]);
                    }
                    else if (key == "UNMUTE")
                    {
                        //SetUnMuteVideo();
                    }
                    else if (key == "UDP_SEND")
                    {
                        SendBroadcastMessage(value);
                    }
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }


        //웹뷰에 값을 전달하기 위한 함수
        private void setCallAppToWeb(string _str)
        {
            m_webview_main.BeginInvoke(new Action(() =>
            {
                if (m_webview_main == null || m_webview_main.CoreWebView2 == null)
                {
                    if (m_webview_main_loaded == false)
                    {
                        //Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>WebView2 초기화 전");
                        return;
                    }
                }
                SetLog("[AppToWeb] " + _str);
                m_webview_main.CoreWebView2.PostWebMessageAsString(_str);
            }));
        }
        private void GetDisplayScale()
        {
            try
            {
                Point pt = new Point(0, 0); // 예: 화면의 좌상단 (0, 0) 위치
                IntPtr hMonitor = MonitorFromPoint(pt, 2); // MONITOR_DEFAULTTONEAREST = 2

                if (GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0) // S_OK = 0
                {
                    Console.WriteLine($"Horizontal DPI: {dpiX}");
                    Console.WriteLine($"Vertical DPI: {dpiY}");

                    // 디스플레이 배율 계산
                    float scaleFactorX = dpiX / 96.0f;
                    float scaleFactorY = dpiY / 96.0f;

                    Console.WriteLine($"Horizontal Scale Factor: {scaleFactorX * 100}%");
                    Console.WriteLine($"Vertical Scale Factor: {scaleFactorY * 100}%");

                    m_display_scale = scaleFactorX;
                    SetLog("[Display Scale] " + m_display_scale);
                }
                else
                {
                    Console.WriteLine("Failed to get DPI for monitor.");
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }

        public void SetHideCursor()
        {
            if (m_mouse_show == true)
            {
                m_mouse_show = false;
                Cursor.Hide();
            }
        }
        public void SetShowCursor()
        {
            if (m_mouse_show == false)
            {
                m_mouse_show = true;
                Cursor.Show();
            }
        }


        private void OnCheckMostTopEvent(object state)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        this.TopMost = true;
                    });
                }
                else
                {
                    this.TopMost = true;
                }
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }

        //ini파일을 읽기 위한 함수
        static Dictionary<string, Dictionary<string, string>> ReadIniFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null; // 파일이 존재하지 않으면 null 반환
            }

            Dictionary<string, Dictionary<string, string>> m_config_ini_data = new Dictionary<string, Dictionary<string, string>>();
            string currentSection = "";

            foreach (string line in File.ReadLines(filePath))
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Trim('[', ']');
                    m_config_ini_data[currentSection] = new Dictionary<string, string>();
                }
                else if (!string.IsNullOrWhiteSpace(line) && line.Contains("="))
                {
                    string[] parts = line.Split(new char[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    m_config_ini_data[currentSection][key] = value;
                }
            }

            return m_config_ini_data;
        }

        //로그를 입력하는 함수
        public void SetLog(string desc)
        {
            string strNowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            desc = "[" + strNowTime + "] " + desc;
            Console.WriteLine(desc);

            m_log_stream_write.WriteLine(desc);
            m_log_stream_write.Flush();
            m_log_file_stream.Flush();

            Int64 fileSize = m_log_file_stream.Length;

            // 파일 사이즈가 1메가가 넘으면 해당 파일 닫은 후 파일 생성
            if (fileSize > 1048576)
            {
                RenameLogFile();
            }
        }
        //새로운 로그파일을 만드는 함수
        public void RenameLogFile()
        {
            try
            {
                string newFileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"; // 새 파일 이름 생성
                string newPath = Path.Combine(m_log_file_path, newFileName); // 새 파일의 경로 생성
                File.Copy(m_log_file_name, newPath, true); // 기존 로그 파일을 새 파일 이름으로 변경
                m_log_stream_write.Write("");
                m_log_stream_write.Flush();
                m_log_file_stream.Flush();
                m_log_stream_write.Close();
                m_log_file_stream.Close();
                File.Delete(m_log_file_name);
                CreateLogFile();
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }
        //로그파일 생성
        public void CreateLogFile()
        {
            try
            {
                string strNowMonth = DateTime.Now.ToString("yyyyMM");
                string strNowTime = DateTime.Now.ToString("yyyyMMdd");

                m_log_file_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs\\" + Process.GetCurrentProcess().ProcessName + "\\" + strNowMonth);
                m_log_file_name = Path.Combine(m_log_file_path, $"log_{strNowTime}.txt");

                // 만약 폴더가 존재하지 않으면 생성
                if (!Directory.Exists(m_log_file_path))
                {
                    Directory.CreateDirectory(m_log_file_path);
                }
                if (Directory.Exists(m_log_file_name))
                {
                    m_log_file_stream = new FileStream(m_log_file_name, FileMode.CreateNew);
                }
                else
                {
                    m_log_file_stream = new FileStream(m_log_file_name, FileMode.Append);
                }
                m_log_stream_write = new StreamWriter(m_log_file_stream, System.Text.Encoding.UTF8);
            }
            catch (Exception exception)
            {
                SetLog("[Exception] <" + System.Reflection.MethodBase.GetCurrentMethod().Name + "> " + exception);
            }
        }


        public class HookManager
        {

            [DllImport("user32.dll")]
            static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);

            [DllImport("user32.dll")]
            static extern bool UnhookWindowsHookEx(IntPtr hInstance);

            [DllImport("user32.dll")]
            static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);

            [DllImport("kernel32.dll")]
            static extern IntPtr LoadLibrary(string lpFileName);

            private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            const int WH_KEYBOARD_LL = 13;
            const int WM_KEYDOWN = 0x100;
            const int WM_KEYUP = 0x0101;
            const int WM_SYSKEYDOWN = 0x0104;
            const int WM_SYSKEYUP = 0x0105;

            static bool isShiftPressed = false; // Shift 상태 추적
            static bool isAltPressed = false;

            private LowLevelKeyboardProc _proc = hookProc;

            private static IntPtr hhook = IntPtr.Zero;

            [DllImport("user32.dll")]
            private static extern int ToUnicode(
                uint wVirtKey,
                uint wScanCode,
                byte[] lpKeyState,
                [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
                int cchBuff,
                uint wFlags);

            [DllImport("user32.dll")]
            private static extern bool GetKeyboardState(byte[] lpKeyState);

            public void SetHook()
            {
                IntPtr hInstance = LoadLibrary("User32");
                hhook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hInstance, 0);
            }

            public void SetUnHook()
            {
                if (hhook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hhook);
                    hhook = IntPtr.Zero; // 해제 후 핸들을 초기화
                }

                UnhookWindowsHookEx(hhook);
            }


            private static void SetPressKeyPad(string _str, string _code)
            {
                Console.WriteLine("SetPressKeyPad : " + _str);
                m_form.setCallAppToWeb("KEYPAD|" + _str);
            }

            public static IntPtr hookProc(int code, IntPtr wParam, IntPtr lParam)
            {
                int vkCode0 = Marshal.ReadInt32(lParam);
                if (code >= 0)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys key = (Keys)vkCode;

                    // Shift 키가 눌렸는지 확인
                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        // Alt 키가 눌렸는지 확인
                        if (key == Keys.LMenu || key == Keys.RMenu)
                        {
                            // Alt 키의 눌림 상태를 처리
                            isAltPressed = true; // Alt 눌림 상태로 변경
                        }

                        //Console.WriteLine(isAltPressed);

                        if (key == Keys.LShiftKey || key == Keys.RShiftKey)
                        {
                            isShiftPressed = true; // Shift 눌림 상태로 변경
                        }

                        // 키 상태 배열을 가져옴 (Shift, Alt, Ctrl 등)
                        byte[] keyboardState = new byte[256];
                        GetKeyboardState(keyboardState);

                        // Shift 상태 반영
                        if (isShiftPressed)
                        {
                            keyboardState[(int)Keys.ShiftKey] = 0x80; // Shift 상태 활성화
                        }

                        // 가상 키 코드를 문자로 변환
                        uint scanCode = (uint)Marshal.ReadInt32(lParam + 8); // lParam의 8바이트는 스캔 코드
                        StringBuilder buffer = new StringBuilder(5);
                        int result = ToUnicode((uint)vkCode, scanCode, keyboardState, buffer, buffer.Capacity, 0);

                        if (result > 0)
                        {
                            // 변환된 문자를 출력
                            //Console.WriteLine($"Key pressed: {buffer.ToString()} {key} {vkCode}");
                            if (key == Keys.Back || key == Keys.Return)
                            {
                                SetPressKeyPad(key.ToString(), vkCode.ToString());
                                //SetTextBox(key.ToString(), vkCode.ToString());
                                return (IntPtr)1;
                            }
                            else
                            {
                                if (m_form.m_hook_check_array.Contains(buffer.ToString()))
                                {
                                    SetPressKeyPad(buffer.ToString(), vkCode.ToString());
                                    //SetTextBox(buffer.ToString(), vkCode.ToString());
                                    return (IntPtr)1;
                                }
                            }
                        }
                        else
                        {
                            //Console.WriteLine($"Key pressed (no char): {buffer.ToString()} {key} {vkCode}");
                            if (m_form.m_hook_check_array.Contains(key.ToString()))
                            {
                                SetPressKeyPad(key.ToString(), vkCode.ToString());
                                //SetTextBox(key.ToString(), vkCode.ToString());
                                return (IntPtr)1;
                            }
                        }
                    }
                    else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                    {
                        if (key == Keys.LShiftKey || key == Keys.RShiftKey)
                        {
                            isShiftPressed = false; // Shift 떼짐 상태로 변경
                        }
                        else if (wParam == (IntPtr)WM_KEYUP && (key == Keys.LMenu || key == Keys.RMenu))
                        {
                            isAltPressed = false; // Alt 떼짐 상태로 변경
                        }
                    }

                    return CallNextHookEx(hhook, code, (int)wParam, lParam);
                    //return (IntPtr)1; // 키 입력을 차단
                }
                else
                {
                    return CallNextHookEx(hhook, code, (int)wParam, lParam);
                }
            }
        }
    }
}
