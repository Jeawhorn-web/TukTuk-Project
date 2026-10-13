using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

#if GLTFAST
using GLTFast;
#endif

/// <summary>
/// Sloyd Unity Bridge for Unity — receives GLB import commands from the Sloyd web app.
/// Starts automatically when Unity loads. Open via Window > Sloyd Unity Bridge.
/// Requires GLTFast package (com.unity.cloud.gltfast) for GLB import.
/// </summary>
[InitializeOnLoad]
public class SloydUnityBridge : EditorWindow
{
    private const int Port = 4573;
    private const string BridgeVersion = "1.0.1";
    private const string GltfastPackageId = "com.unity.cloud.gltfast";

    private static readonly string[] AllowedOrigins = {
        "https://app.sloyd.ai",
        "https://blue.app.sloyd.ai",
        "https://green.app.sloyd.ai",
        "https://staging.app.sloyd.ai",
    };

    private static HttpListener _listener;
    private static Thread _listenerThread;
    private static bool _isRunning;

    private static readonly ConcurrentQueue<(string url, string name)> _pendingImports =
        new ConcurrentQueue<(string, string)>();

    private static bool _gltfastAvailable;
    private static bool _installPromptShown;
    private static AddRequest _addRequest;
    private static bool _isInstalling;

    // Processing flags for UI and Logic
    private static bool _isProcessingImport = false;
    private static string _currentImportName = "";

    static SloydUnityBridge()
    {
        _gltfastAvailable = CheckGltfastAvailable();
        EditorApplication.delayCall += AutoStart;
    }

    private static bool CheckGltfastAvailable()
    {
#if GLTFAST
        return true;
#else
        return false;
#endif
    }

    private static void AutoStart()
    {
        if (!_gltfastAvailable && !_installPromptShown)
        {
            _installPromptShown = true;
            PromptInstallGltfast();
        }
        if (!_isRunning) StartServer();
    }

    private static void PromptInstallGltfast()
    {
        var result = EditorUtility.DisplayDialog(
            "Sloyd Unity Bridge - Missing Dependency",
            "GLTFast package is required for importing 3D models from Sloyd.\n\n" +
            "Would you like to install it now?\n\n" +
            "Package: com.unity.cloud.gltfast",
            "Install GLTFast",
            "Later"
        );

        if (result)
        {
            InstallGltfast();
        }
        else
        {
            Debug.LogWarning("[SloydUnityBridge] GLTFast not installed. You can install it later via Window > Package Manager or Window > Sloyd Unity Bridge.");
        }
    }

    private static void InstallGltfast()
    {
        if (_isInstalling)
        {
            Debug.Log("[SloydUnityBridge] GLTFast installation already in progress...");
            return;
        }

        Debug.Log("[SloydUnityBridge] Installing GLTFast package...");
        _isInstalling = true;
        _addRequest = Client.Add(GltfastPackageId);
        EditorApplication.update += InstallProgress;
    }

    private static void InstallProgress()
    {
        if (_addRequest == null || !_addRequest.IsCompleted)
            return;

        EditorApplication.update -= InstallProgress;
        _isInstalling = false;

        if (_addRequest.Status == StatusCode.Success)
        {
            Debug.Log($"[SloydUnityBridge] GLTFast installed successfully: {_addRequest.Result.packageId}");
            EditorUtility.DisplayDialog(
                "Sloyd Unity Bridge",
                "GLTFast has been installed successfully!\n\n" +
                "Unity will recompile scripts. The Sloyd Unity Bridge will be fully functional after recompilation.",
                "OK"
            );
        }
        else if (_addRequest.Status >= StatusCode.Failure)
        {
            Debug.LogError($"[SloydUnityBridge] Failed to install GLTFast: {_addRequest.Error.message}");
            EditorUtility.DisplayDialog(
                "Sloyd Unity Bridge - Installation Failed",
                $"Failed to install GLTFast:\n{_addRequest.Error.message}\n\n" +
                "Please try installing manually via Window > Package Manager.",
                "OK"
            );
        }

        _addRequest = null;
    }

    [MenuItem("Window/Sloyd Unity Bridge")]
    public static void ShowWindow() => GetWindow<SloydUnityBridge>("Sloyd Unity Bridge");

    // NEW: Forces the Editor Window to redraw so our animations actually play
    private void Update()
    {
        if (_isInstalling || _isProcessingImport)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Sloyd Unity Bridge", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // --- GLTFAST INSTALLATION UI ---
        if (!_gltfastAvailable)
        {
            EditorGUILayout.HelpBox(
                "GLTFast package is required for GLB import.",
                MessageType.Warning);

            EditorGUI.BeginDisabledGroup(_isInstalling);
            if (GUILayout.Button(_isInstalling ? "Installing..." : "Install GLTFast"))
            {
                InstallGltfast();
            }
            EditorGUI.EndDisabledGroup();

            if (_isInstalling)
            {
                // Create an animated ellipsis based on time
                int dots = Mathf.FloorToInt((float)EditorApplication.timeSinceStartup * 3f) % 4;
                EditorGUILayout.HelpBox($"Installing GLTFast{new string('.', dots)}\nPlease wait, Unity will recompile when finished.", MessageType.Info);
            }

            GUILayout.Space(10);
        }

        // --- SERVER STATUS UI ---
        GUILayout.Label(_isRunning ? $"Listening on http://localhost:{Port}" : "Server stopped.");
        if (_isRunning && GUILayout.Button("Stop Server")) StopServer();
        else if (!_isRunning && GUILayout.Button("Start Server")) StartServer();

        GUILayout.Space(15);

        // --- ACTIVE IMPORT UI ---
        if (_isProcessingImport && !string.IsNullOrEmpty(_currentImportName))
        {
            int dots = Mathf.FloorToInt((float)EditorApplication.timeSinceStartup * 3f) % 4;
            EditorGUILayout.HelpBox($"Currently importing: {_currentImportName}{new string('.', dots)}", MessageType.Info);
        }
        else if (_pendingImports.Count > 0)
        {
            EditorGUILayout.HelpBox($"Files waiting in queue: {_pendingImports.Count}", MessageType.None);
        }
    }

    private static void StartServer()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _isRunning = true;
            _listenerThread = new Thread(ListenLoop) { IsBackground = true };
            _listenerThread.Start();
            EditorApplication.update += DrainImportQueue;
            Debug.Log($"[SloydUnityBridge] Listening on http://localhost:{Port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SloydUnityBridge] Failed to start: {ex.Message}");
        }
    }

    private static void StopServer()
    {
        _isRunning = false;
        EditorApplication.update -= DrainImportQueue;
        try { _listener?.Stop(); } catch { }
        _listener = null;
        Debug.Log("[SloydUnityBridge] Server stopped.");
    }

    private static void DrainImportQueue()
    {
        if (!_isProcessingImport && _pendingImports.TryDequeue(out var item))
        {
            _isProcessingImport = true;
            ProcessImportAsync(item.url, item.name);
        }
    }

    private static void ListenLoop()
    {
        while (_isRunning)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch (HttpListenerException) { break; }
            catch (Exception ex) { Debug.LogError($"[SloydUnityBridge] Listener error: {ex.Message}"); }
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;
        var origin = req.Headers["Origin"] ?? "";
        try
        {
            SetCorsHeaders(res, origin);
            if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; res.Close(); return; }

            if (req.HttpMethod == "GET" && req.Url.AbsolutePath == "/ping")
            {
                SendJson(res, 200, $"{{\"unity\":\"{Application.unityVersion}\",\"enabled\":true,\"pluginVersion\":\"{BridgeVersion}\"}}");
                return;
            }

            if (req.HttpMethod == "POST")
            {
                string body;
                using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();

                var url = ExtractJsonString(body, "url");
                var name = ExtractJsonString(body, "name") ?? "";

                if (string.IsNullOrEmpty(url) || !url.EndsWith(".glb"))
                {
                    SendJson(res, 400, "{\"error\":\"url must end with .glb\"}");
                    return;
                }

                if (!_gltfastAvailable)
                {
                    SendJson(res, 500, "{\"error\":\"GLTFast package not installed. Open Window > Sloyd Unity Bridge in Unity to install it.\"}");
                    return;
                }

                SendJson(res, 200, "{\"isSuccess\":true}");

                _pendingImports.Enqueue((url, name));
                return;
            }

            res.StatusCode = 404; res.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SloydUnityBridge] Request error: {ex.Message}");
            try { res.StatusCode = 500; res.Close(); } catch { }
        }
    }

    // -------------------------------------------------------------------------
    // Download + import GLB using GLTFast's native Unity importer
    // -------------------------------------------------------------------------

    private static async void ProcessImportAsync(string glbUrl, string modelName)
    {
#if !GLTFAST
        Debug.LogError("[SloydUnityBridge] GLTFast package not installed. Open Window > Sloyd Unity Bridge to install it.");
        _isProcessingImport = false;
        return;
#else
        try
        {
            var uri = new Uri(glbUrl);
            var fileName = string.IsNullOrEmpty(modelName)
                ? Path.GetFileNameWithoutExtension(uri.AbsolutePath)
                : modelName;

            if (string.IsNullOrEmpty(fileName)) fileName = "SloydModel";
            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');

            // NEW: Update UI state with the current model's name
            _currentImportName = fileName;

            using var client = new HttpClient();
            var glbBytes = await client.GetByteArrayAsync(glbUrl);

            var destDir = Path.Combine(Application.dataPath, "SloydModels", fileName);
            Directory.CreateDirectory(destDir);

            var absoluteGlbPath = Path.Combine(destDir, $"{fileName}.glb");
            var relativeGlbPath = $"Assets/SloydModels/{fileName}/{fileName}.glb";

            File.WriteAllBytes(absoluteGlbPath, glbBytes);

            // Refresh to make Unity aware of the new file.
            // GLTFast's ScriptedImporter will automatically import it as a model!
            AssetDatabase.Refresh();

            EditorApplication.delayCall += () =>
            {
                // Load the imported asset directly from the AssetDatabase
                var glbAsset = AssetDatabase.LoadAssetAtPath<GameObject>(relativeGlbPath);

                if (glbAsset != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(glbAsset);
                    go.name = fileName;
                    go.transform.position = Vector3.zero;
                    Selection.activeGameObject = go;

                    Debug.Log($"[SloydUnityBridge] '{fileName}' successfully downloaded and added to scene.");
                }
                else
                {
                    Debug.LogError($"[SloydUnityBridge] Failed to load imported GLB asset at {relativeGlbPath}. Ensure GLTFast is configured to import .glb files.");
                }
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SloydUnityBridge] Import failed: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // NEW: Clear the UI state so the label goes away, and free up the queue
            _currentImportName = "";
            _isProcessingImport = false;
        }
#endif
    }

    // -------------------------------------------------------------------------
    // CORS / HTTP helpers
    // -------------------------------------------------------------------------

    private static void SetCorsHeaders(HttpListenerResponse res, string origin)
    {
        var norm = origin.TrimEnd('/');
        bool ok = norm.StartsWith("http://localhost") || norm.StartsWith("http://127.0.0.1")
                  || AllowedOrigins.Contains(norm);
        res.Headers["Access-Control-Allow-Origin"] = ok ? origin : AllowedOrigins[1];
        res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        res.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, contenttype, x-device-id, x-is-mobile, x-mixpanel-id";
        // Required for Chrome's Private Network Access (PNA) - allows public origins to access localhost
        res.Headers["Access-Control-Allow-Private-Network"] = "true";
    }

    private static void SendJson(HttpListenerResponse res, int code, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        res.StatusCode = code;
        res.ContentType = "application/json";
        res.ContentLength64 = bytes.Length;
        res.OutputStream.Write(bytes, 0, bytes.Length);
        res.Close();
    }

    private static string ExtractJsonString(string json, string key)
    {
        var search = $"\"{key}\"";
        var idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return null;
        idx = json.IndexOf('"', idx + search.Length);
        if (idx < 0) return null;
        var end = json.IndexOf('"', idx + 1);
        if (end < 0) return null;
        return json.Substring(idx + 1, end - idx - 1);
    }
}
