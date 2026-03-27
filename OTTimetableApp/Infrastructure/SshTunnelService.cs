using Renci.SshNet;

namespace OTTimetableApp.Infrastructure;

public class SshTunnelService : IDisposable
{
    private SshClient? _client;
    private ForwardedPortLocal? _forwardedPort;
    private bool _disposed;

    public bool IsActive => _client?.IsConnected == true;

    public void Start(AppConfig cfg)
    {
        Stop();

        if (!cfg.SshEnabled) return;

        AuthenticationMethod auth = !string.IsNullOrWhiteSpace(cfg.SshPrivateKeyPath)
            ? new PrivateKeyAuthenticationMethod(cfg.SshUser, new PrivateKeyFile(cfg.SshPrivateKeyPath))
            : new PasswordAuthenticationMethod(cfg.SshUser, cfg.SshPassword);

        var connInfo = new ConnectionInfo(cfg.SshHost, cfg.SshPort, cfg.SshUser, auth);
        _client = new SshClient(connInfo);
        _client.Connect();

        _forwardedPort = new ForwardedPortLocal(
            "127.0.0.1", (uint)cfg.SshLocalPort,
            cfg.SshRemoteHost, (uint)cfg.SshRemotePort);

        _client.AddForwardedPort(_forwardedPort);
        _forwardedPort.Start();
    }

    public void Stop()
    {
        _forwardedPort?.Stop();
        _forwardedPort = null;
        _client?.Disconnect();
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
