using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;


namespace HardwareMonitorApp
{
    // Data Transfer Objects for JSON serialization
    public class HardwareMonitorData
    {
        public OperatingSystemInfo OS { get; set; }
        public HardwareDevices Devices { get; set; } = new HardwareDevices();
    }

    public class OperatingSystemInfo
    {
        public string OSName { get; set; }
        public string OSVersion { get; set; }
        public string Is64Bit { get; set; }
    }
    public class HardwareDevices
    {
        public CPU CPU { get; set; }
        public List<GPU> GPU { get; set; } = new List<GPU>();
        public Motherboard Motherboard { get; set; }
        public HardwareDevice RAM { get; set; }
    }
    public class HardwareDevice
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<SensorData> Sensors { get; set; } = new List<SensorData>();
    }
    public class CPU
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<SensorData> Frequency { get; set; } = new List<SensorData>();
        public List<SensorData> Power { get; set; } = new List<SensorData>();
        public List<SensorData> Temperature { get; set; } = new List<SensorData>();
        public List<SensorData> Voltage { get; set; } = new List<SensorData>();
        public List<SensorData> Load { get; set; } = new List<SensorData>();
    }

    public class GPU
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<SensorData> Frequency { get; set; } = new List<SensorData>();
        public List<SensorData> Power { get; set; } = new List<SensorData>();
        public List<SensorData> Temperature { get; set; } = new List<SensorData>();
        public List<SensorData> Voltage { get; set; } = new List<SensorData>();
        public List<SensorData> Load { get; set; } = new List<SensorData>();
        public List<SensorData> Memory { get; set; } = new List<SensorData>();
        public List<SensorData> Fan { get; set; } = new List<SensorData>();
    }
    public class Motherboard
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<SensorData> Power { get; set; } = new List<SensorData>();
        public List<SensorData> Temperature { get; set; } = new List<SensorData>();
        public List<SensorData> Voltage { get; set; } = new List<SensorData>();
        public List<SensorData> Fan { get; set; } = new List<SensorData>();
    }
    public class SensorData
    {
        public string Name { get; set; }
        public float Value { get; set; }
        public string Unit { get; set; }
    }

    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
                subHardware.Accept(this);
        }

        public void VisitSensor(ISensor sensor) 
        { 
        }
        public void VisitParameter(IParameter parameter)
        {
        }
    }

    class Program
    {
        private static readonly List<WebSocket> _connectedClients = new List<WebSocket>();
        private static readonly object _clientsLock = new object();
        private static string _latestJsonOutput = string.Empty;
        private static readonly object _jsonLock = new object();

        static async Task Main(string[] args)
        {
            // Initialize the Computer object
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };

            try
            {
                computer.Open();

                // Start WebSocket server
                var cancellationTokenSource = new CancellationTokenSource();
                var webSocketServerTask = StartWebSocketServer(cancellationTokenSource.Token);
                var httpServerTask = StartHttpServer(cancellationTokenSource.Token);

                Console.WriteLine("WebSocket server started on ws://localhost:42069");
                Console.WriteLine("HTTP server started on http://localhost:6969/monitor");
                Console.WriteLine("Press Ctrl+C to stop...");

                // Run continuously until terminated
                while (true)
                {
                    try
                    {
                        string jsonOutput = GetHardwareData(computer);

                        // Update the latest JSON output
                        lock (_jsonLock)
                        {
                            _latestJsonOutput = jsonOutput;
                        }

                        // Clear and write JSON to console
                        //Console.Clear();
                        //Console.WriteLine(jsonOutput);

                        // Send to all connected WebSocket clients
                        await BroadcastToClients(jsonOutput);
                    }
                    catch (Exception ex)
                    {
                        // Output error as JSON for consistent parsing
                        var errorData = new
                        {
                            error = true,
                            message = ex.Message,
                            stackTrace = ex.StackTrace
                        };
                        Console.Clear();
                        Console.WriteLine(JsonSerializer.Serialize(errorData));
                    }

                    // Wait for 100 milliseconds
                    //await Task.Delay(100);
                }
            }
            finally
            {
                computer.Close();
            }
        }

        private static async Task StartHttpServer(CancellationToken cancellationToken)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:6969/");
            listener.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleGetRequest(context), cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HTTP server error: {ex.Message}");
                }
            }
        }

        private static void HandleGetRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/monitor")
                {
                    string jsonOutput;
                    lock (_jsonLock)
                    {
                        jsonOutput = _latestJsonOutput;
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(jsonOutput);
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
                else
                {
                    response.StatusCode = 404;
                    byte[] buffer = Encoding.UTF8.GetBytes("Not Found");
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP request handling error: {ex.Message}");
            }
        }

        private static async Task StartWebSocketServer(CancellationToken cancellationToken)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:42069/");
            listener.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(async () => await HandleWebSocketConnection(context), cancellationToken);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket server error: {ex.Message}");
                }
            }
        }

        private static async Task HandleWebSocketConnection(HttpListenerContext context)
        {
            WebSocket webSocket = null;
            try
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                webSocket = webSocketContext.WebSocket;

                lock (_clientsLock)
                {
                    _connectedClients.Add(webSocket);
                }

                Console.WriteLine($"Client connected. Total clients: {_connectedClients.Count}");

                var buffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket connection error: {ex.Message}");
            }
            finally
            {
                if (webSocket != null)
                {
                    lock (_clientsLock)
                    {
                        _connectedClients.Remove(webSocket);
                    }
                    Console.WriteLine($"Client disconnected. Total clients: {_connectedClients.Count}");
                    webSocket?.Dispose();
                }
            }
        }

        private static async Task BroadcastToClients(string jsonOutput)
        {
            var buffer = Encoding.UTF8.GetBytes(jsonOutput);
            var segment = new ArraySegment<byte>(buffer);

            List<WebSocket> clientsToRemove = new List<WebSocket>();

            lock (_clientsLock)
            {
                foreach (var client in _connectedClients.ToList())
                {
                    if (client.State == WebSocketState.Open)
                    {
                        try
                        {
                            client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch
                        {
                            clientsToRemove.Add(client);
                        }
                    }
                    else
                    {
                        clientsToRemove.Add(client);
                    }
                }

                foreach (var client in clientsToRemove)
                {
                    _connectedClients.Remove(client);
                }
            }
        }

        public static OperatingSystemInfo GetOperatingSystemInfo()
        {
            var osInfo = new OperatingSystemInfo
            {
                OSName = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                OSVersion = Environment.OSVersion.VersionString,
                Is64Bit = Environment.Is64BitOperatingSystem ? "Yes" : "No"
            };
            return osInfo;
        }
        public static string GetHardwareData(Computer computer)
        {
            var monitorData = new HardwareMonitorData
            {
                OS = GetOperatingSystemInfo()
            };

            // Create visitor to update all sensors
            var updateVisitor = new UpdateVisitor();
            computer.Accept(updateVisitor);
            // Iterate through all hardware components

            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    // Process CPU
                    var cpu = new CPU
                    {
                        Name = hardware.Name,
                        Type = GetHardwareTypeString(hardware.HardwareType),
                        Description = GetHardwareDescription(hardware)
                    };

                    ProcessCpuSensors(hardware, cpu);

                    // Also process sub-hardware
                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        ProcessCpuSensors(subHardware, cpu);
                    }

                    monitorData.Devices.CPU = cpu;
                }
                else if (IsGpuHardware(hardware.HardwareType))
                {
                    // Process GPU
                    var gpu = new GPU
                    {
                        Name = hardware.Name,
                        Type = GetHardwareTypeString(hardware.HardwareType),
                        Description = GetHardwareDescription(hardware)
                    };

                    ProcessGpuSensors(hardware, gpu);

                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        ProcessGpuSensors(subHardware, gpu);
                    }

                    monitorData.Devices.GPU.Add(gpu);
                }
                else if (hardware.HardwareType == HardwareType.Motherboard)
                {
                    // Process Motherboard
                    var motherboard = new Motherboard
                    {
                        Name = hardware.Name,
                        Type = GetHardwareTypeString(hardware.HardwareType),
                        Description = GetHardwareDescription(hardware)
                    };

                    ProcessMotherboardSensors(hardware, motherboard);

                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        ProcessMotherboardSensors(subHardware, motherboard);
                    }

                    monitorData.Devices.Motherboard = motherboard;
                }
                else if (hardware.HardwareType == HardwareType.Memory)
                {
                    // Process RAM
                    var device = new HardwareDevice
                    {
                        Name = hardware.Name,
                        Type = GetHardwareTypeString(hardware.HardwareType),
                        Description = GetHardwareDescription(hardware)
                    };

                    ProcessSensors(hardware, device);

                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        ProcessSensors(subHardware, device);
                    }

                    if (device.Sensors.Count > 0)
                    {
                        monitorData.Devices.RAM = device;
                    }
                }
            }

            // Serialize to JSON with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            return JsonSerializer.Serialize(monitorData, options);
        }

        private static void ProcessCpuSensors(IHardware hardware, CPU cpu)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (!FilterSensor(sensor))
                    continue;

                var sensorData = new SensorData
                {
                    Name = sensor.Name,
                    Value = sensor.Value.Value,
                    Unit = GetSensorUnit(sensor.SensorType)
                };

                // Sort sensors by type into appropriate lists
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        cpu.Temperature.Add(sensorData);
                        break;
                    case SensorType.Power:
                        cpu.Power.Add(sensorData);
                        break;
                    case SensorType.Voltage:
                        cpu.Voltage.Add(sensorData);
                        break;
                    case SensorType.Clock:
                    case SensorType.Frequency:
                        cpu.Frequency.Add(sensorData);
                        break;
                    case SensorType.Load:
                        cpu.Load.Add(sensorData);
                        break;
                }
            }
        }

        private static void ProcessGpuSensors(IHardware hardware, GPU gpu)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (!FilterSensor(sensor))
                    continue;

                var sensorData = new SensorData
                {
                    Name = sensor.Name,
                    Value = sensor.Value.Value,
                    Unit = GetSensorUnit(sensor.SensorType)
                };

                // Sort sensors by type into appropriate lists
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        gpu.Temperature.Add(sensorData);
                        break;
                    case SensorType.Power:
                        gpu.Power.Add(sensorData);
                        break;
                    case SensorType.Voltage:
                        gpu.Voltage.Add(sensorData);
                        break;
                    case SensorType.Clock:
                    case SensorType.Frequency:
                        gpu.Frequency.Add(sensorData);
                        break;
                    case SensorType.Load:
                        gpu.Load.Add(sensorData);
                        break;
                    case SensorType.Fan:
                        gpu.Fan.Add(sensorData);
                        break;
                    case SensorType.SmallData:
                    case SensorType.Data:
                    case SensorType.Throughput:
                        gpu.Memory.Add(sensorData);
                        break;
                }
            }
        }

        private static void ProcessMotherboardSensors(IHardware hardware, Motherboard motherboard)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (!FilterSensor(sensor))
                    continue;

                var sensorData = new SensorData
                {
                    Name = sensor.Name,
                    Value = sensor.Value.Value,
                    Unit = GetSensorUnit(sensor.SensorType)
                };

                // Sort sensors by type into appropriate lists
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        motherboard.Temperature.Add(sensorData);
                        break;
                    case SensorType.Power:
                        motherboard.Power.Add(sensorData);
                        break;
                    case SensorType.Voltage:
                        motherboard.Voltage.Add(sensorData);
                        break;
                    case SensorType.Fan:
                        motherboard.Fan.Add(sensorData);
                        break;
                }
            }
        }

        private static void ProcessSensors(IHardware hardware, HardwareDevice device)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {

                if (FilterSensor(sensor))
                {
                    device.Sensors.Add(new SensorData
                    {
                        Name = sensor.Name,
                        Value = sensor.Value.Value,
                        Unit = GetSensorUnit(sensor.SensorType)
                    });
                }
                //}
            }
        }

        private static bool FilterSensor(ISensor sensor)
        {
            return sensor.Value.HasValue &&
                !float.IsNaN(sensor.Value.Value) &&
                !float.IsInfinity(sensor.Value.Value) &&
                !sensor.Name.Contains("D3D");

        }

        private static string GetHardwareDescription(IHardware hardware)
        {
            if (IsGpuHardware(hardware.HardwareType))
            {
                // Intel GPUs are typically integrated
                if (hardware.HardwareType == HardwareType.GpuIntel)
                {
                    return "Integrated GPU";
                }

                // For NVIDIA and AMD, check the name for common integrated GPU identifiers
                string nameLower = hardware.Name.ToLower();
                if (nameLower.Contains("integrated") ||
                    nameLower.Contains("radeon(tm) graphics") || // AMD APU integrated graphics
                    nameLower.Contains("vega") && nameLower.Contains("graphics")) // AMD Ryzen integrated
                {
                    return "Integrated GPU";
                }

                return "Dedicated GPU";
            }

            return hardware.Properties.ToString() ?? "";
        }

        private static bool IsGpuHardware(HardwareType type)
        {
            return type == HardwareType.GpuNvidia ||
                   type == HardwareType.GpuAmd ||
                   type == HardwareType.GpuIntel;
        }

        private static string GetHardwareTypeString(HardwareType type)
        {
            return type switch
            {
                HardwareType.Cpu => "CPU",
                HardwareType.GpuNvidia => "GPU-NVIDIA",
                HardwareType.GpuAmd => "GPU-AMD",
                HardwareType.GpuIntel => "GPU-INTEL",
                HardwareType.Memory => "Memory",
                HardwareType.Motherboard => "Motherboard",
                HardwareType.Storage => "Storage",
                HardwareType.Network => "Network",

                _ => type.ToString()
            };
        }

        private static string GetSensorUnit(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => "C",
                SensorType.Load => "%",
                SensorType.Clock => "MHz",
                SensorType.Voltage => "Volts",
                SensorType.Power => "Watts",
                SensorType.Fan => "RPM",
                SensorType.Data => "GB",
                SensorType.Throughput => "MB/s",
                SensorType.Frequency => "MHz",
                _ => type.ToString()
            };
        }
    }
}