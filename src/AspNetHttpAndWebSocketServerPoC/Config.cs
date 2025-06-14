using System;
using AspNetEphemeralHttpServerPoC.Extensions;
using Mono.Unix;

namespace AspNetEphemeralHttpServerPoC;

internal enum AppEnv
{
    Development,
    Staging,
    Production,
}

internal static class Config
{
    static Config()
    {
        var rawAppEnv = Environment.GetEnvironmentVariable("APP_ENV");
        if (string.IsNullOrEmpty(rawAppEnv)) AppEnv = AppEnv.Development;
        else if (!Enum.TryParse(rawAppEnv, true, out AppEnv)) {
            throw new ArgumentException($"APP_ENV value '{rawAppEnv}' is not acceptable");
        }
    }

    public static readonly AppEnv AppEnv;
    public static UnixFileInfo SocketInfo {
        get {
            if (AppEnv is AppEnv.Development) return new UnixFileInfo("/tmp/steam-auth.sock");
            return new UnixFileInfo("/var/run/steam-auth/steam-auth.sock");
        }
    }

    public static void ConfigureSocket()
    {
        if (AppEnv is AppEnv.Development) {
            // in development, the executor should have admin privileges and/or belong to the nginx group, so chown the socket
            SocketInfo.SetOwner(SocketInfo.OwnerUser, new UnixGroupInfo("nginx"));
            return;
        }

        // in production, the process owner will be a dedicated service user
        // give `nginx` access to the socket via ACLs
        SocketInfo.GetFileAccessControl()
            .AccessAcl
            .ModifyUser(new UnixUserInfo("nginx"), AclPermissions.Read | AclPermissions.Write);
    }
}
