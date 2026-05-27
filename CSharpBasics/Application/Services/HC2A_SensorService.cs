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
    private bool _isOpen;

    public HC2A_SensorService(string portName)
    {
        _portName = portName;
    }

    public bool IsOpen => _isOpen;

    public void Open()
    {
        _isOpen = true;
    }

    public void Close()
    {
        _isOpen = false;
    }

    public void Dispose()
    {
        Close();
    }

    public HC2A_Reading Read()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("HC2A sensor service is not open.");
        }

        string response = ReadRawResponseOnce();

        if (string.IsNullOrWhiteSpace(response))
        {
            throw new TimeoutException(
                "No response received from HC2A sensor.");
        }

        return HC2A_ResponseParser.Parse(response);
    }

    private string ReadRawResponseOnce()
    {
        using var serialPort = new SerialPort(_portName, BaudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = TimeoutMilliseconds,
            WriteTimeout = TimeoutMilliseconds
        };

        serialPort.Open();

        serialPort.DiscardInBuffer();
        serialPort.DiscardOutBuffer();

        serialPort.Write(ReadCommand);

        Thread.Sleep(ResponseWaitMilliseconds);

        return serialPort.ReadExisting();
    }

    public async Task<HC2A_Reading> ReadAsync()
    {
        return await Task.Run(() => Read());
    }
}
