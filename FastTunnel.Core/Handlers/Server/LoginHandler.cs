// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     https://github.com/FastTunnel/FastTunnel/edit/v2/LICENSE
// Copyright (c) 2019 Gui.H

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastTunnel.Core.Extensions;
using FastTunnel.Core.Listener;
using FastTunnel.Core.Models;
using FastTunnel.Core.Models.Massage;
using FastTunnel.Core.Server;
using Microsoft.Extensions.Logging;

namespace FastTunnel.Core.Handlers.Server;

public class LoginHandler : ILoginHandler
{
    private readonly ILogger logger;
    public const bool NeedRecive = true;

    public LoginHandler(ILogger<LoginHandler> logger)
    {
        this.logger = logger;
    }

    protected async Task HandleLoginAsync(FastTunnelServer server, TunnelClient client, LogInMassage requet, CancellationToken cancellationToken)
    {
        var hasTunnel = false;

        await client.webSocket.SendCmdAsync(MessageType.Log, $"穿透协议 | 映射关系（公网=>内网）", cancellationToken);
        Thread.Sleep(300);

        if (requet.Webs != null && requet.Webs.Any())
        {
            hasTunnel = true;
            foreach (var item in requet.Webs)
            {
                var hostName = $"{item.SubDomain}.{server.ServerOption.CurrentValue.WebDomain}".Trim().ToLower();
                var info = new WebInfo { Socket = client.webSocket, WebConfig = item };

                logger.LogDebug($"New Http '{hostName}'");
                server.WebList.AddOrUpdate(hostName, info, (key, oldInfo) => { return info; });

                await client.webSocket.SendCmdAsync(MessageType.Log, $"  HTTP   | http://{hostName}:{client.ConnectionPort} => {item.LocalIp}:{item.LocalPort}", CancellationToken.None);
                client.AddWeb(info);

                if (item.WWW != null)
                {
                    foreach (var www in item.WWW)
                    {
                        // TODO:validateDomain
                        hostName = www.Trim().ToLower();
                        server.WebList.AddOrUpdate(www, info, (key, oldInfo) => { return info; });

                        await client.webSocket.SendCmdAsync(MessageType.Log, $"  HTTP   | http://{www}:{client.ConnectionPort} => {item.LocalIp}:{item.LocalPort}", CancellationToken.None);
                        client.AddWeb(info);
                    }
                }
            }
        }

        if (requet.Forwards != null && requet.Forwards.Any())
        {
            if (server.ServerOption.CurrentValue.EnableForward)
            {
                hasTunnel = true;

                foreach (var item in requet.Forwards)
                {
                    try
                    {
                        if (server.ForwardList.TryGetValue(item.RemotePort, out var old))
                        {
                            logger.LogDebug($"Remove Listener {old.Listener.ListenIp}:{old.Listener.ListenPort}");
                            old.Listener.Stop();
                            server.ForwardList.TryRemove(item.RemotePort, out var _);
                        }

                        // TODO: 客户端离线时销毁
                        var ls = new PortProxyListener("0.0.0.0", item.RemotePort, logger, client.webSocket);
                        ls.Start(new ForwardDispatcher(logger, server, item));

                        var forwardInfo = new ForwardInfo<ForwardHandlerArg> { Listener = ls, Socket = client.webSocket, SSHConfig = item };

                        // TODO: 客户端离线时销毁
                        server.ForwardList.TryAdd(item.RemotePort, forwardInfo);
                        logger.LogDebug($"SSH proxy success: {item.RemotePort} => {item.LocalIp}:{item.LocalPort}");

                        client.AddForward(forwardInfo);
                        await client.webSocket.SendCmdAsync(MessageType.Log, $"  TCP    | {server.ServerOption.CurrentValue.WebDomain}:{item.RemotePort} => {item.LocalIp}:{item.LocalPort}", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"SSH proxy error: {item.RemotePort} => {item.LocalIp}:{item.LocalPort}");
                        logger.LogError(ex.Message);
                        await client.webSocket.SendCmdAsync(MessageType.Log, ex.Message, CancellationToken.None);
                        continue;
                    }
                }
            }
            else
            {
                await client.webSocket.SendCmdAsync(MessageType.Log, TunnelResource.ForwardDisabled, CancellationToken.None);
            }
        }

        if (!hasTunnel)
            await client.webSocket.SendCmdAsync(MessageType.Log, TunnelResource.NoTunnel, CancellationToken.None);
    }

    public virtual async Task<bool> HandlerMsg(FastTunnelServer fastTunnelServer, TunnelClient tunnelClient, string lineCmd, CancellationToken cancellationToken)
    {
        var msg = JsonSerializer.Deserialize<LogInMassage>(lineCmd);
        await HandleLoginAsync(fastTunnelServer, tunnelClient, msg, cancellationToken);
        return NeedRecive;
    }
}
