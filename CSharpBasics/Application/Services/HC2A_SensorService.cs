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
    private const int ResponseWaitMilliseconds = 500;
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

    public HC2A_Reading Read()
    {
        if (!_serialPort.IsOpen)
        {
            throw new InvalidOperationException("HC2A sensor service is not open.");
        }
        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();

        _serialPort.Write(ReadCommand);

        Thread.Sleep(ResponseWaitMilliseconds);

        string response = _serialPort.ReadExisting();

        if (string.IsNullOrWhiteSpace(response))
        {
            throw new TimeoutException("No response received from HC2A sensor.");
        }
        return HC2A_ResponseParser.Parse(response);
    }

    public async Task<HC2A_Reading> ReadAsync()
    {
        return await Task.Run(() => Read());
    }
}