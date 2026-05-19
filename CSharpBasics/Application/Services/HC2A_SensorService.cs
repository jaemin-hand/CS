using CSharpBasics.Application.Parsers;
using CSharpBasics.Domain.Models;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpBasics.Application.Services;


public class HC2A_SensorService : IDisposable
{
    private const int BaudRate = 19200;
    private const int TimeoutMilliseconds = 3000;
    private const int PollIntervalMilliseconds = 20;
    // How many ms to check the buffer

    private const int ResponseIdleMilliseconds = 50;
    // If there is no data for a few ms after the last data reception,
    // do you want to see it as response complete

    private const string ReadCommand = "{F99RDD}\r";
    private readonly string _portName;
    private readonly SerialPort _serialPort;
    public HC2A_SensorService(string portName)
    {
        _portName = portName;
        
        _serialPort = new SerialPort(_portName, BaudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = TimeoutMilliseconds,
            WriteTimeout = TimeoutMilliseconds
        };
    }

    public bool IsOpen => _serialPort.IsOpen;

    public void Open()
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
    }

    public void Close()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }
    }

    public void Dispose()
    {
        Close();
        _serialPort.Dispose();
    }

    private string WaitForResponse()
    {
        var startedAt = DateTime.UtcNow;
        var lastReceivedAt = DateTime.UtcNow;
        var response = string.Empty;
        var hasReceivedAnyData = false;

        while ((DateTime.UtcNow - startedAt).TotalMilliseconds < TimeoutMilliseconds)
        {
            string chunk = _serialPort.ReadExisting();

            if (!string.IsNullOrEmpty(chunk))
            {
                response += chunk;
                hasReceivedAnyData = true;
                lastReceivedAt = DateTime.UtcNow; 
            }

            if (hasReceivedAnyData && (DateTime.UtcNow - lastReceivedAt).TotalMilliseconds >= ResponseIdleMilliseconds)
            {
                return response;
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }

        throw new TimeoutException("No complete response received from HC2A sensor.");
    }

    public HC2A_Reading Read()
    {
        if (!_serialPort.IsOpen)
        {
            throw new InvalidOperationException("HC2A sensor service is not open.");
        }
        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();

        _serialPort.Write(ReadCommand);

        string response = WaitForResponse();

        return HC2A_ResponseParser.Parse(response);
    }

    public async Task<HC2A_Reading> ReadAsync()
    {
        return await Task.Run(() => Read());
    }
}