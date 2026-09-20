using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PuzzleOnline.Editor
{
    [InitializeOnLoad]
    public static class PuzzleServerMenu
    {
        private const string SessionKey = "PuzzleOnline_ServerPid";
        private const int ServerPort = 7777;
        private static Process _serverProcess;

        static PuzzleServerMenu()
        {
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting()
        {
            StopServer();
        }

        [MenuItem("Tools/Puzzle Online/Start Local Java Server", false, 10)]
        public static void StartServer()
        {
            if (IsPortInUse(ServerPort))
            {
                Debug.LogWarning($"[PuzzleServer] Port {ServerPort} is already in use. Cleaning up previous instance...");
                KillProcessOnPort(ServerPort);
                System.Threading.Thread.Sleep(600);
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string serverDir = Path.Combine(projectRoot, "server");
            string scriptPath = Path.Combine(serverDir, "run-server.ps1");

            if (!File.Exists(scriptPath))
            {
                Debug.LogError($"[PuzzleServer] Script not found: {scriptPath}");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                WorkingDirectory = serverDir,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            try
            {
                _serverProcess = Process.Start(startInfo);
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    SessionState.SetInt(SessionKey, _serverProcess.Id);
                    Debug.Log($"[PuzzleServer] Started server process (PID {_serverProcess.Id}) on port {ServerPort}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PuzzleServer] Failed to start server: {ex.Message}");
            }
        }

        [MenuItem("Tools/Puzzle Online/Stop Local Java Server", false, 11)]
        public static void StopServer()
        {
            int pid = SessionState.GetInt(SessionKey, 0);
            if (pid > 0)
            {
                KillProcessTree(pid);
                SessionState.SetInt(SessionKey, 0);
            }

            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    KillProcessTree(_serverProcess.Id);
                }
                catch { }
                _serverProcess = null;
            }

            // Always make sure port 7777 is freed from any orphaned java process
            KillProcessOnPort(ServerPort);
            Debug.Log($"[PuzzleServer] Stopped server and freed port {ServerPort}.");
        }

        [MenuItem("Tools/Puzzle Online/Restart Local Java Server", false, 12)]
        public static void RestartServer()
        {
            StopServer();
            System.Threading.Thread.Sleep(800);
            StartServer();
        }

        public static bool IsPortInUse(int port)
        {
            try
            {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var listeners = ipProperties.GetActiveTcpListeners();
                foreach (var ep in listeners)
                {
                    if (ep.Port == port) return true;
                }
            }
            catch (Exception)
            {
                // Fallback
            }
            return false;
        }

        private static void KillProcessTree(int pid)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/F /T /PID {pid}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            catch { }
        }

        private static void KillProcessOnPort(int port)
        {
            try
            {
                // PowerShell one-liner to kill any process listening on the specified port
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Get-NetTCPConnection -LocalPort {port} -ErrorAction SilentlyContinue | ForEach-Object {{ Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PuzzleServer] Could not force kill process on port {port}: {ex.Message}");
            }
        }

        public static bool IsServerRunning()
        {
            if (IsPortInUse(ServerPort)) return true;

            int pid = SessionState.GetInt(SessionKey, 0);
            if (pid <= 0) return false;

            try
            {
                Process p = Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch
            {
                SessionState.SetInt(SessionKey, 0);
                return false;
            }
        }
    }
}
