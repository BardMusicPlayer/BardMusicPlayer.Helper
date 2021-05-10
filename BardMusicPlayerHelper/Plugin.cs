/*
 * Copyright(c) 2021 MoogleTroupe, 2018-2020 parulina
 * Licensed under the GPL v3 license. See https://github.com/BardMusicPlayer/BardMusicPlayer/blob/develop/LICENSE for full license information.
 */

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Plugin;
using H.Pipes;
using H.Pipes.Args;

namespace BardMusicPlayerHelper
{
    public class Plugin : IDalamudPlugin
    {
        private PipeClient<string> _pipe;

        private DalamudPluginInterface _pluginInterface;

        private delegate IntPtr Ui(IntPtr basePtr);

        private delegate void ChatBox(IntPtr uiModule, IntPtr message, IntPtr unused1, byte unused2);

        private Ui _ui;

        private ChatBox _chatBox;

        private IntPtr _uiPointer;

        private bool _scanned;

        public string Name => "BardMusicPlayer Helper";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;
            
            _pipe = new PipeClient<string>("BardMusicPlayer-Grunt-Dalamud") { AutoReconnect = true };
            _pipe.MessageReceived += OnMessage;
            _pipe.Disconnected += OnDisconnected;
            _pipe.Connected += OnConnected;

            _scanned = ScanChatSignatures();

            _pipe.ConnectAsync();

            PluginLog.Information("Loaded");
        }

        private void OnMessage(object sender, ConnectionMessageEventArgs<string?> e)
        {
            if (string.IsNullOrEmpty(e.Message)) return;
            _pipe.WriteAsync(Process.GetCurrentProcess().Id + ":chatted:" + SendChat(e.Message));
        }

        private void OnDisconnected(object sender, ConnectionEventArgs<string> e)
        {
            PluginLog.Information("Disconnected");
            _pipe.ConnectAsync();
        }

        private void OnConnected(object sender, ConnectionEventArgs<string> e)
        {
            PluginLog.Information("Connected");
            _pipe.WriteAsync(Process.GetCurrentProcess().Id + ":scanned:" + _scanned);
        }

        private bool SendChat(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text) || !_pluginInterface.ClientState.IsLoggedIn) return false;

                text = Encoding.UTF8.GetString(Convert.FromBase64String(text));

                if (_ui == null || _chatBox == null || _uiPointer == IntPtr.Zero)
                {
                    PluginLog.Error("SendChat Signature Error");
                    return false;
                }
                var ui = _ui(Marshal.ReadIntPtr(_uiPointer));
                if (ui == IntPtr.Zero)
                {
                    PluginLog.Error("SendChat Signature Error");
                    return false;
                }
                using var structure = new ChatLine(text);
                var message = Marshal.AllocHGlobal(400);
                Marshal.StructureToPtr(structure, message, false);
                _chatBox(ui, message, IntPtr.Zero, 0);
                Marshal.FreeHGlobal(message);
            }
            catch (Exception ex)
            {
                PluginLog.Error("SendChat Error: " + ex.Message);
                return false;
            }
            return true;
        }

        private bool ScanChatSignatures()
        {
            if (_scanned) return true;
            try
            {
                _ui = Marshal.GetDelegateForFunctionPointer<Ui>(_pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0"));
                _chatBox = Marshal.GetDelegateForFunctionPointer<ChatBox>(_pluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9"));
                _uiPointer = _pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8 ?? ?? ?? ??");
            }
            catch (Exception ex)
            {
                PluginLog.Error("ScanChatSignatures Error: " + ex.Message);
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (_pipe != null)
            {
                try
                {
                    _pipe.DisconnectAsync().GetAwaiter().GetResult();
                    _pipe.MessageReceived -= OnMessage;
                    _pipe.Disconnected -= OnDisconnected;
                    _pipe.Connected -= OnConnected;
                    _pipe.DisposeAsync();
                }
                catch (Exception)
                {
                    // Ignored
                }
            }
            _pluginInterface = null;
        }
    }
}
