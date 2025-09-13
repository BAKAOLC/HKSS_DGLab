#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Logging;
using DGLabCSharp;
using DGLabCSharp.Enums;
using DGLabCSharp.Structs;
using UnityEngine;

namespace HKSS_DGLab
{
    /// <summary>
    ///     DGLab设备控制器，用于管理WebSocket服务器和处理游戏事件
    /// </summary>
    public class DGLabController(ManualLogSource logger) : IDisposable
    {
        private readonly ManualLogSource _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private DGLabCSharp.DGLabController? _controller;
        private bool _disposed;
        private DGLabWebSocketServer? _server;

        public bool IsInitialized { get; private set; }

        public bool HasConnectedApps => _controller?.GetConnectedApps().Count > 0;
        public string QRCodePath { get; private set; } = "";

        /// <summary>
        ///     释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // 停止并释放服务器
                if (_server != null)
                {
                    _server.StopAsync().Wait(TimeSpan.FromSeconds(5));
                    _server.Dispose();
                }

                IsInitialized = false;
                _disposed = true;

                _logger.LogInfo("DGLab控制器已释放");
            }
            catch (Exception ex)
            {
                _logger.LogError($"释放DGLab控制器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        ///     初始化DGLab服务器
        /// </summary>
        public async Task<bool> InitializeAsync(int port = 9999)
        {
            if (IsInitialized)
            {
                _logger.LogWarning("DGLab控制器已经初始化");
                return true;
            }

            try
            {
                // 检查端口可用性
                if (!DGLabWebSocketServer.IsPortAvailable(port))
                {
                    _logger.LogWarning($"端口 {port} 不可用，正在寻找可用端口...");
                    port = DGLabWebSocketServer.FindAvailablePort();
                    if (port == -1)
                    {
                        _logger.LogError("未找到可用端口");
                        return false;
                    }

                    _logger.LogInfo($"找到可用端口: {port}");
                }

                // 创建服务器和控制器
                _server = new(port);
                _controller = new(_server);

                // 订阅服务器事件
                SubscribeToServerEvents();

                // 启动服务器
                await _server.StartAsync();

                // 生成连接信息和固定二维码
                var localIP = NetworkUtils.GetLocalIPAddress();
                if (!string.IsNullOrEmpty(localIP))
                {
                    _logger.LogInfo($"DGLab服务器已启动 - 地址: {localIP}:{port}");
                    _logger.LogInfo($"控制器ID: {_server.ControllerClientId}");
                    _logger.LogInfo($"连接URL: ws://{localIP}:{port}/{_server.ControllerClientId}");

                    // 生成固定的二维码文件
                    try
                    {
                        QRCodePath = Path.Combine(Application.persistentDataPath, "dglab_qr.png");
                        QRCodeGenerator.GenerateConnectionQRFile(localIP, port, _server.ControllerClientId, QRCodePath);
                        _logger.LogInfo($"连接二维码已保存到: {QRCodePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"生成二维码失败: {ex.Message}");
                        // 如果生成失败，尝试使用默认路径
                        try
                        {
                            QRCodeGenerator.GenerateConnectionQRFile(localIP, port, _server.ControllerClientId);
                            QRCodePath = Path.Combine(Directory.GetCurrentDirectory(), "dglab_connection_qr.png");
                            _logger.LogInfo($"连接二维码已保存到默认路径: {QRCodePath}");
                        }
                        catch
                        {
                            _logger.LogError("无法生成二维码文件");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("无法获取局域网IP地址");
                    _logger.LogInfo($"手动连接URL: ws://YOUR_IP:{port}/{_server.ControllerClientId}");
                }

                IsInitialized = true;
                _logger.LogInfo("DGLab控制器初始化成功，等待APP连接...");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DGLab控制器初始化失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     订阅服务器事件
        /// </summary>
        private void SubscribeToServerEvents()
        {
            if (_server == null) return;

            _server.ClientConnected += (_, args) =>
            {
                var (clientId, type, ipAddress, port) = args;
                _logger.LogInfo($"新的{type}已连接 - ID: {clientId[..8]}... 来自: {ipAddress}:{port}");
            };

            _server.ClientDisconnected += (_, clientId) => { _logger.LogInfo($"客户端已断开: {clientId[..8]}..."); };

            _server.ServerError += (_, ex) => { _logger.LogError($"服务器错误: {ex.Message}"); };

            _server.ErrorOccurred += (_, args) =>
            {
                var (clientId, error) = args;
                _logger.LogError($"客户端错误 - ID: {clientId[..8]}..., 错误: {error.Message}");
            };

            // 订阅消息处理器事件
            _server.MessageHandler.BindingSucceeded += (_, args) =>
            {
                var (clientId, message) = args;
                _logger.LogInfo($"绑定成功: {message}");
            };

            _server.MessageHandler.BindingFailed += (_, args) =>
            {
                var (clientId, error) = args;
                _logger.LogWarning($"绑定失败: {error}");
            };
        }

        /// <summary>
        ///     发送波形到所有连接的APP的指定通道
        /// </summary>
        public async Task<bool> SendWaveAsync(WaveType waveType, Channel channel, int duration = 3)
        {
            if (!IsInitialized || _server == null || _controller == null)
            {
                _logger.LogWarning("DGLab控制器未初始化");
                return false;
            }

            var apps = _controller.GetConnectedApps();
            if (apps.Count == 0)
            {
                _logger.LogWarning("没有已连接的APP");
                return false;
            }

            var boundApps = _server.ClientManager.GetControllerBoundApps(_server.ControllerClientId);
            if (boundApps.Count == 0)
            {
                _logger.LogWarning("没有已绑定的APP");
                return false;
            }

            try
            {
                var targetApps = apps.Where(app => boundApps.Contains(app.Id)).ToList();
                if (targetApps.Count == 0)
                {
                    _logger.LogWarning("没有可发送的目标APP");
                    return false;
                }

                // 每秒发送1次，持续指定秒数
                const int punishmentTime = 1; // 每秒发送次数
                var totalSends = punishmentTime * duration; // 总发送次数
                const int timeSpace = 1000 / punishmentTime; // 发送间隔（毫秒）

                var waveData = WaveData.GetWaveDataJson(waveType);
                var messageContent = $"{channel.ToChannelString()}:{waveData}";

                var successCount = 0;

                // 循环发送波形消息
                for (var i = 0; i < totalSends; i++)
                {
                    var tasks = targetApps.Select(app =>
                    {
                        var message = new ClientMessage(messageContent, duration, channel, _server.ControllerClientId,
                            app.Id);
                        return _server.SendMessageToClientAsync(app.Id, message);
                    }).ToList();

                    var results = await Task.WhenAll(tasks);
                    var currentSuccessCount = results.Count(r => r);

                    if (i == 0)
                    {
                        successCount = currentSuccessCount;
                        _logger.LogInfo(
                            $"已向 {successCount}/{targetApps.Count} 个APP的{channel}通道发送{waveType}波形，持续{duration}秒");
                    }

                    if (i < totalSends - 1) await Task.Delay(timeSpace);
                }

                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"发送波形时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     发送波形到所有通道
        /// </summary>
        public async Task<bool> SendWaveToAllChannelsAsync(WaveType waveType, int duration = 3)
        {
            var taskA = SendWaveAsync(waveType, Channel.A, duration);
            var taskB = SendWaveAsync(waveType, Channel.B, duration);

            var results = await Task.WhenAll(taskA, taskB);
            return results[0] || results[1]; // 至少一个成功就返回true
        }

        /// <summary>
        ///     紧急停止所有输出
        /// </summary>
        public async Task<bool> EmergencyStopAsync()
        {
            if (!IsInitialized || _server == null || _controller == null) return false;

            var apps = _controller.GetConnectedApps();
            if (apps.Count == 0) return false;

            try
            {
                var tasks = new List<Task<bool>>();

                foreach (var app in apps)
                {
                    var boundApps = _server.ClientManager.GetControllerBoundApps(_server.ControllerClientId);
                    if (!boundApps.Contains(app.Id)) continue;

                    // 清除所有通道
                    var clearA = new WebSocketMessage
                    {
                        Type = MessageType.Msg.ToTypeString(),
                        ClientId = _server.ControllerClientId,
                        TargetId = app.Id,
                        Message = "clear-1",
                    };
                    var clearB = new WebSocketMessage
                    {
                        Type = MessageType.Msg.ToTypeString(),
                        ClientId = _server.ControllerClientId,
                        TargetId = app.Id,
                        Message = "clear-2",
                    };

                    // 设置强度为0
                    var strengthA = new StrengthMessage(StrengthOperationType.SetToZero, (int)Channel.A, 0,
                        _server.ControllerClientId, app.Id);
                    var strengthB = new StrengthMessage(StrengthOperationType.SetToZero, (int)Channel.B, 0,
                        _server.ControllerClientId, app.Id);

                    tasks.Add(_server.SendMessageToClientAsync(app.Id, clearA));
                    tasks.Add(_server.SendMessageToClientAsync(app.Id, clearB));
                    tasks.Add(_server.SendMessageToClientAsync(app.Id, strengthA));
                    tasks.Add(_server.SendMessageToClientAsync(app.Id, strengthB));
                }

                var results = await Task.WhenAll(tasks);
                var success = Array.TrueForAll(results, r => r);

                if (success)
                    _logger.LogInfo("紧急停止执行成功");
                else
                    _logger.LogWarning("紧急停止执行部分失败");

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"紧急停止时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     获取连接状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            if (!IsInitialized || _server == null || _controller == null) return "DGLab控制器未初始化";

            var apps = _controller.GetConnectedApps();
            var totalClients = _server.ClientManager.GetActiveClientCount();

            return $"服务器端口: {_server.Port}, 活跃客户端: {totalClients}, 已连接APP: {apps.Count}";
        }
    }
}