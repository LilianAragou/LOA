// FileLogger.cs
// Capture tous les logs Unity (Log/Warning/Error/Exception) et les écrit
// dans un fichier texte par session dans <BuildFolder>/log/.
// Fallback automatique vers <persistentDataPath>/log/ si le build folder
// n'est pas accessible en écriture (ex: macOS .app dans /Applications).

using UnityEngine;
using System;
using System.IO;
using System.Text;

#if !UNITY_WEBGL
[DefaultExecutionOrder(-32000)]
public class FileLogger : MonoBehaviour
{
    private static FileLogger _instance;
    private static readonly object _lock = new object();

    private StreamWriter _writer;
    private string _logPath;
    private string _logDir;

    [Header("Options")]
    [Tooltip("Préfixe des noms de fichiers de log.")]
    [SerializeField] private string fileNamePrefix = "PlayerLog";
    [Tooltip("Ne pas tronquer un ancien fichier si le nom correspond (rare)")]
    [SerializeField] private bool appendToExisting = false;
    [Tooltip("Inclure la stacktrace aussi pour les Log (non recommandé)")]
    [SerializeField] private bool includeStackTraceForLogs = false;
    [Tooltip("Inclure la stacktrace pour les Warning")]
    [SerializeField] private bool includeStackTraceForWarnings = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("~FileLogger");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<FileLogger>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            // 1) Dossier cible: <BuildFolder>/log/
            _logDir = ComputeBuildLogDirectory();

            // 2) Essaie de créer <BuildFolder>/log/ ; si échec -> fallback persistentDataPath/log
            if (!TryEnsureDirectory(_logDir))
            {
                string fallback = Path.Combine(Application.persistentDataPath, "log");
                if (!TryEnsureDirectory(fallback))
                    throw new Exception("Impossible de créer un dossier de log (build et fallback).");

                _logDir = fallback;
            }

            // 3) Chemin de fichier (timestamp)
            var ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var fileName = $"{fileNamePrefix}_{ts}.txt";
            _logPath = Path.Combine(_logDir, fileName);

            // 4) Ouverture du fichier en partage lecture
            var fs = new FileStream(
                _logPath,
                appendToExisting ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.Read
            );
            _writer = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = true };

            // 5) En-tête de session
            lock (_lock)
            {
                _writer.WriteLine($"===== Session start {DateTime.Now:O} =====");
                _writer.WriteLine($"Unity {Application.unityVersion} | {Application.platform} | {Application.productName} {Application.version}");
                _writer.WriteLine($"Company: {Application.companyName}");
                _writer.WriteLine($"Build Log Dir: {_logDir}");
                _writer.WriteLine($"PersistentDataPath: {Application.persistentDataPath}");
                _writer.WriteLine("===============================================");
            }

            // 6) Abonnement thread-safe
            Application.logMessageReceivedThreaded += HandleLogThreaded;

            Debug.Log($"[FileLogger] Logging to: {_logPath}");
        }
        catch (Exception e)
        {
            Debug.LogError("[FileLogger] Initialisation échouée: " + e);
        }
    }

    private void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= HandleLogThreaded;
        CloseWriter();
    }

    private void OnApplicationQuit()
    {
        try
        {
            lock (_lock)
            {
                _writer?.WriteLine($"===== Session end {DateTime.Now:O} =====");
            }
        }
        finally
        {
            CloseWriter();
        }
    }

    // --- Core logging ---
    private void HandleLogThreaded(string condition, string stackTrace, LogType type)
    {
        var w = _writer;
        if (w == null) return;

        var now = DateTime.Now.ToString("HH:mm:ss.fff");
        var sb = new StringBuilder(512);
        sb.Append('[').Append(now).Append("] ")
          .Append(type.ToString().ToUpper()).Append(": ")
          .AppendLine(condition);

        bool needStack =
            type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert ||
            (type == LogType.Warning && includeStackTraceForWarnings) ||
            (type == LogType.Log && includeStackTraceForLogs);

        if (needStack && !string.IsNullOrEmpty(stackTrace))
            sb.AppendLine(stackTrace);

        lock (_lock)
        {
            try { w.Write(sb.ToString()); }
            catch { /* éviter une récursion si écriture échoue */ }
        }
    }

    private void CloseWriter()
    {
        lock (_lock)
        {
            try { _writer?.Flush(); } catch { }
            try { _writer?.Dispose(); } catch { }
            _writer = null;
        }
    }

    // --- Helpers de chemin ---
    /// <summary>
    /// Retourne le dossier de log "à côté du build".
    /// Windows/Linux: dossier contenant l'exécutable.
    /// macOS: dossier contenant le .app (un cran AU-DESSUS du .app).
    /// </summary>
    private static string ComputeBuildLogDirectory()
    {
        // Application.dataPath:
        // - Windows/Linux: "<BuildFolder>/<AppName>_Data"
        // - macOS: "<BuildFolder>/<AppName>.app/Contents"
        string dataPath = Application.dataPath;
        if (string.IsNullOrEmpty(dataPath))
            return Path.Combine(Application.persistentDataPath, "log");

        var dataDir = new DirectoryInfo(dataPath);
        var parent = dataDir.Parent;         // Windows/Linux => BuildFolder ; macOS => .../<AppName>.app
        if (parent == null)
            return Path.Combine(Application.persistentDataPath, "log");

        string buildFolder = parent.FullName;

        // Sur macOS, on veut le dossier QUI CONTIENT le .app
        // dataPath = ".../MyApp.app/Contents" -> parent = ".../MyApp.app" -> remonter encore d'un cran
        if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            if (buildFolder.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                var up = Directory.GetParent(buildFolder);
                if (up != null) buildFolder = up.FullName;
            }
        }

        return Path.Combine(buildFolder, "log");
    }

    private static bool TryEnsureDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            // Test rapide d'écriture : crée/supprime un fichier vide
            string probe = Path.Combine(path, ".write_test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Chemin complet du fichier de log de la session courante.</summary>
    public static string CurrentLogPath => _instance ? _instance._logPath : string.Empty;

    /// <summary>Dossier où le log est effectivement écrit (build/log ou fallback).</summary>
    public static string CurrentLogDirectory => _instance ? _instance._logDir : string.Empty;
}
#else
public class FileLogger : MonoBehaviour
{
    // WebGL: pas d'accès FS classique -> logger désactivé.
}
#endif
