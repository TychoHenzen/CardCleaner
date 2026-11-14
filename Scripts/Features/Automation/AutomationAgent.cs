using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     The "Automation" autoload. It does nothing unless the run was started with <c>-- --automation-port=N</c> in a
///     debug build (<see cref="AutomationLaunch" />). Then it listens on 127.0.0.1:N for JSON lines
///     (<see cref="AutomationProtocol" />) and runs each command in order on the main thread, also while the tree is
///     paused. tools/e2e/e2e.mjs is the client; tools/e2e/README.md lists the commands.
/// </summary>
public partial class AutomationAgent : Node
{
    private const string Loopback = "127.0.0.1";

    private readonly List<Client> _clients = [];
    private readonly Queue<(Client Client, string Line)> _pending = new();
    private AutomationCommands? _commands;
    private TcpServer? _server;
    private bool _running;

    public override void _Ready()
    {
        if (AutomationLaunch.Port is not { } port)
        {
            SetProcess(false);
            return;
        }

        ProcessMode = ProcessModeEnum.Always;
        _commands = new AutomationCommands(this);
        _server = new TcpServer();
        var error = _server.Listen((ushort)port, Loopback);
        if (error != Error.Ok)
        {
            ILog.Error($"automation: cannot listen on {Loopback}:{port}: {error}");
            SetProcess(false);
            return;
        }

        ILog.Print($"automation: listening on {Loopback}:{port}");
    }

    public override void _Process(double delta)
    {
        while (_server!.IsConnectionAvailable())
            _clients.Add(new Client(_server.TakeConnection()));
        foreach (var client in _clients)
        {
            foreach (var line in client.ReadLines())
                _pending.Enqueue((client, line));
        }

        _clients.RemoveAll(c => c.Closed && !IsWaitedOn(c));
        if (!_running && _pending.Count > 0)
            _ = RunNext();
    }

    public override void _ExitTree() => _server?.Stop();

    private bool IsWaitedOn(Client client)
    {
        foreach (var (waiting, _) in _pending)
        {
            if (waiting == client)
                return true;
        }

        return false;
    }

    private async Task RunNext()
    {
        _running = true;
        while (_pending.Count > 0)
        {
            var (client, line) = _pending.Dequeue();
            string reply;
            try
            {
                reply = await _commands!.Execute(AutomationProtocol.Parse(line));
            }
            catch (AutomationException e)
            {
                reply = AutomationProtocol.Failure(null, e.Message);
            }

            client.Send(reply);
        }

        _running = false;
    }

    /// <summary>One connection: buffers bytes into lines and writes replies, one JSON object per line.</summary>
    private sealed class Client(StreamPeerTcp peer)
    {
        private readonly StringBuilder _buffer = new();
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

        public bool Closed => peer.GetStatus() is StreamPeerTcp.Status.None or StreamPeerTcp.Status.Error;

        public IEnumerable<string> ReadLines()
        {
            peer.Poll();
            if (peer.GetStatus() != StreamPeerTcp.Status.Connected)
                yield break;
            var available = peer.GetAvailableBytes();
            if (available > 0)
            {
                var bytes = peer.GetData(available)[1].AsByteArray();
                var chars = new char[_decoder.GetCharCount(bytes, 0, bytes.Length)];
                _decoder.GetChars(bytes, 0, bytes.Length, chars, 0);
                _buffer.Append(chars);
            }

            var text = _buffer.ToString();
            var end = text.LastIndexOf('\n');
            if (end < 0)
                yield break;
            _buffer.Clear().Append(text[(end + 1)..]);
            foreach (var line in text[..end].Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    yield return line.Trim();
            }
        }

        public void Send(string reply)
        {
            peer.Poll();
            if (peer.GetStatus() == StreamPeerTcp.Status.Connected)
                peer.PutData(Encoding.UTF8.GetBytes(reply + "\n"));
        }
    }
}
