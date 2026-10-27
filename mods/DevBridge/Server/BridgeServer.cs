using System;
using System.Runtime.ExceptionServices;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using ObenFramework.Values;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevBridge.Server;

/// <summary>
/// Newline-delimited JSON over TCP. Each request line is {"id", "method", "params"}; each answer is
/// {"id", "ok", "result"} or {"id", "ok": false, "error"}. Socket work happens on background
/// threads; commands always run on Unity's main thread.
/// </summary>
public sealed class BridgeServer : IDisposable
{
    private readonly BridgeConfig _config;
    private readonly CommandRouter _router;
    private readonly ModLogger _log;
    private readonly List<TcpClient> _clients = new List<TcpClient>();
    private TcpListener _listener;
    private Thread _acceptThread;
    private volatile bool _running;

    public BridgeServer(BridgeConfig config, CommandRouter router, ModLogger log)
    {
        _config = config;
        _router = router;
        _log = log;
    }

    /// <summary>The configured port, moved up by one for each extra game instance (27861 for the second).</summary>
    private int Port => _config.Port + Instance.Number - 1;

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Parse(_config.Address), Port);
        _listener.Start();
        _running = true;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "DevBridge accept" };
        _acceptThread.Start();
        _log.Info($"Listening on {_config.Address}:{Port}");
    }

    public void Dispose()
    {
        _running = false;
        try { _listener?.Stop(); } catch (SocketException) { }
        lock (_clients)
        {
            foreach (var c in _clients)
                c.Close();
            _clients.Clear();
        }
        _acceptThread?.Join(1000);
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch (Exception) when (!_running)
            {
                return;
            }
            catch (SocketException e)
            {
                _log.Warn("Accept failed: " + e.Message);
                continue;
            }

            lock (_clients)
                _clients.Add(client);
            new Thread(() => Serve(client)) { IsBackground = true, Name = "DevBridge client" }.Start();
        }
    }

    private void Serve(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, new UTF8Encoding(false));
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

            string line;
            while (_running && (line = reader.ReadLine()) != null)
            {
                if (line.Length > 0)
                    writer.WriteLine(Handle(line).ToString(Formatting.None));
            }
        }
        catch (IOException)
        {
            // Client went away.
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            lock (_clients)
                _clients.Remove(client);
            client.Close();
        }
    }

    private JObject Handle(string line)
    {
        JToken id = null;
        try
        {
            var request = JObject.Parse(line);
            id = request["id"];
            string method = request.Value<string>("method") ?? throw new ArgumentException("Missing 'method'.");
            var parameters = request["params"] as JObject ?? new JObject();

            object result = RunOnMainThread(method, parameters);
            return new JObject { ["id"] = id, ["ok"] = true, ["result"] = JsonValues.ToJson(result, 3) };
        }
        catch (Exception e)
        {
            var root = e.GetBaseException();
            var error = new JObject { ["id"] = id, ["ok"] = false, ["error"] = Describe(root) };
            // Errors the commands throw on purpose explain themselves; anything from deeper down gets its stack.
            if (root.StackTrace != null && root.TargetSite?.DeclaringType?.Assembly != typeof(BridgeServer).Assembly)
                error["stack"] = root.StackTrace;
            return error;
        }
    }

    // Some game exceptions have no message, so name the type then.
    private static string Describe(Exception e) =>
        string.IsNullOrEmpty(e.Message) ? e.GetType().FullName : e.Message;

    private object RunOnMainThread(string method, JObject parameters)
    {
        var call = new Deferred();
        MainThread.Post(() =>
        {
            try
            {
                var result = _router.Execute(method, parameters);
                if (result is Deferred inner)
                    ForwardWhenDone(inner, call);
                else
                    call.Complete(result);
            }
            catch (Exception e)
            {
                call.Fail(e);
            }
        });

        if (!call.Wait(_config.TimeoutMs))
            throw new TimeoutException($"'{method}' didn't finish within {_config.TimeoutMs} ms.");
        if (call.Exception != null)
            ExceptionDispatchInfo.Capture(call.Exception).Throw();
        if (call.Error != null)
            throw new InvalidOperationException(call.Error);
        return call.Result;
    }

    private void ForwardWhenDone(Deferred inner, Deferred outer)
    {
        // The inner result is completed from the main thread later; wait for it off-thread.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (!inner.Wait(_config.TimeoutMs))
                outer.Fail("Timed out.");
            else if (inner.Exception != null)
                outer.Fail(inner.Exception);
            else if (inner.Error != null)
                outer.Fail(inner.Error);
            else
                outer.Complete(inner.Result);
        });
    }
}
