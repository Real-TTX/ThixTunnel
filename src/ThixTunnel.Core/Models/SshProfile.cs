namespace ThixTunnel.Core.Models;

/// <summary>How ssh authenticates against the server of a profile.</summary>
public enum SshAuthenticationMethod
{
    /// <summary>Keys of the SSH agent and the default key files of the user (~/.ssh).</summary>
    Agent = 0,

    /// <summary>One specific private key file.</summary>
    KeyFile = 1,

    /// <summary>Password, asked for when connecting. Not possible for automatic connections.</summary>
    Password = 2,
}

/// <summary>The sign-in to exactly one SSH server. One profile can be used in many routes.</summary>
public sealed class SshProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public string HostName { get; set; } = "";

    public int Port { get; set; } = 22;

    public string UserName { get; set; } = "";

    public SshAuthenticationMethod AuthenticationMethod { get; set; } = SshAuthenticationMethod.Agent;

    /// <summary>Path of the private key; only used with <see cref="SshAuthenticationMethod.KeyFile"/>.</summary>
    public string KeyFilePath { get; set; } = "";

    public string Description { get; set; } = "";

    public string DisplayAddress => Port == 22 ? $"{UserName}@{HostName}" : $"{UserName}@{HostName}:{Port}";

    public SshProfile Clone() => (SshProfile)MemberwiseClone();
}
