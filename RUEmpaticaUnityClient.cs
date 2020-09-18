using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class RUEmpaticaUnityClient : MonoBehaviour
{
    public string logMessage;
    public string hostName = "Localhost";
    public string ipAddress = "127.0.0.1";
    public string portNum = "28000";
    private bool isConnected, logToFile, devicesAvailable, deviceConnected, streamsSubscribed = false;
    public int selGridInt = -1; //no device selected
    public string[] availableDevices = { };
    private List<string> streamsSelected = new List<string>();
    private static string deviceName, filename = "";

    public TcpClient client;
    NetworkStream stream;
    StreamWriter streamWriter;
    StreamReader streamReader;
    Stopwatch stopWatch;

    public static Toggle acc, bvp, gsr, ibi, tmp, hr, bat, tag;
    public List<Toggle> toggles = new List<Toggle> { acc, bvp, gsr, ibi, tmp, hr, bat, tag };

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (logToFile) { LogData(); };
    }

    void OnGUI()
    {
        if (GUILayout.Button("Connect"))
        {
            PrintToConsole("Connecting to server...");
            Connect(ipAddress, portNum);
            isConnected = true;
        }

        if (isConnected && GUILayout.Button("Disconnect"))
        {
            if (!isConnected || !deviceConnected) return;
            PrintToConsole("Disconnecting device and connection...");
            WriteToServer("device_disconnect");
            Disconnect();
        }

        if (isConnected && !deviceConnected && GUILayout.Button("Available Devices"))
        {
            availableDevices = ListAvailability("device_list").ToArray();
            devicesAvailable = availableDevices.Length > 0;
        }

        if (devicesAvailable)
        {
            //show available devices, can only connect to one device at a time
            GUILayout.BeginHorizontal("Box");
            selGridInt = GUILayout.SelectionGrid(selGridInt, availableDevices, 1);
            GUILayout.EndHorizontal();
        }

        if (selGridInt > -1)
        {
            WriteToServer($"device_connect {availableDevices[selGridInt].ToLower()}");
            if (ReadFromServer().Contains("R device_connect OK"))
            {
                deviceConnected = true;
                PrintToConsole($"Connected to {availableDevices[selGridInt]}");
                deviceName = availableDevices[selGridInt];
                filename = $@"Assets/Scripts/E4_{deviceName}_{DateTime.UtcNow.ToString().Replace(" ", "_").Replace(":", "_")}.txt";
            }
            selGridInt = -1;
        }

        //select desired streams FIRST then press button
        if (isConnected && deviceConnected && GUILayout.Button("Select Streams"))
        {
            foreach (var toggle in toggles)
            {
                if (toggle.isOn)
                {
                    streamsSelected.Add(toggle.name);
                }
            }
        }

        if (streamsSelected.Count > 0 && GUILayout.Button("Start Streaming"))
        {
            foreach (var stream in streamsSelected)
            {
                WriteToServer($"device_subscribe {stream} ON"); //each stream must be subscribed to individually
            }
            streamsSubscribed = true;
            PrintToConsole($"Subscribed to {string.Join(", ", streamsSelected.ToArray())}");
        }

        if (isConnected && streamsSubscribed && deviceConnected && GUILayout.Button("Log Data"))
        {
            logToFile = true;
            File.Create(filename);
            PrintToConsole($"Logging to file: {filename}");            
        }
    }

    public void Connect(string ip, string port)
    {
        try
        {
            client = new TcpClient(ip, int.Parse(port)) { NoDelay = true };
            stream = client.GetStream();
            streamWriter = new StreamWriter(stream);
            streamReader = new StreamReader(stream);
            stopWatch = Stopwatch.StartNew();
            PrintToConsole("Connected to server");
        }
        catch(SocketException e)
        {
            PrintToConsole($"Socket error: {e}");
        }
        
    }

    public void Disconnect()
    {
        if (!isConnected) return;
        streamWriter.Close();
        streamReader.Close();
        client.Close();

        //reset all flags
        isConnected = false;
        devicesAvailable = false;
        deviceConnected = false;
        streamsSubscribed = false;
        streamsSelected.Clear();
        logToFile = false;
    }

    public void LogData()
    {
        var msgToLog = ReadFromServer().Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
        using (StreamWriter sw = new StreamWriter(filename, true))
        {
            foreach (var line in msgToLog)
            {
                sw.WriteLine(line);
                var lineTimestamped = $"{line}{stopWatch.Elapsed.ToString()}{DateTime.UtcNow}";
                PrintToConsole(lineTimestamped);
            }
        }
    }

    public string ReadFromServer()
    {
        var result = "";
        if (!stream.DataAvailable) return result;
        Byte[] data = new Byte[client.SendBufferSize];
        stream.Read(data, 0, data.Length);
        result += System.Text.Encoding.ASCII.GetString(data).TrimEnd('\0');
        return result;
    }

    public void WriteToServer(string message)
    {
        streamWriter.Write($"{message}{Environment.NewLine}");
        streamWriter.Flush();
    }

    public List<string> ListAvailability(string message)
    {
        var msgToReturn = new List<string>();
        WriteToServer(message);
        var msgReceived = ReadFromServer().Split('|').Skip(1).ToList();
        if (msgReceived.Count > 0) //if data received 
        {
            foreach (var msg in msgReceived)
            {
                var msgTrimmed = msg.Replace("Empatica_E4", "").Trim();
                msgToReturn.Add(msgTrimmed);
            }
            PrintToConsole($"Devices available: {string.Join(", ", msgToReturn)}");
        }
        else PrintToConsole("Retrieving data...Try again.");
        return msgToReturn;
    }

    public void PrintToConsole(string message)
    {
        UnityEngine.Debug.Log(message);
    }



}
