#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace CopperCampTools
{
    /// <summary>Writes compiler messages and Console errors to Logs/cc_compile.txt and Logs/cc_console.txt,
    /// so they can be read without looking at the Console. Lives in its own assembly so it still runs
    /// when the game scripts fail to compile.</summary>
    [InitializeOnLoad]
    static class CompileLogger
    {
        static readonly string CompilePath = Path.Combine("Logs", "cc_compile.txt");
        static readonly string ConsolePath = Path.Combine("Logs", "cc_console.txt");

        static CompileLogger()
        {
            CompilationPipeline.compilationStarted -= Started;
            CompilationPipeline.compilationStarted += Started;
            CompilationPipeline.assemblyCompilationFinished -= Finished;
            CompilationPipeline.assemblyCompilationFinished += Finished;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            File.AppendAllText(CompilePath, System.DateTime.Now.ToString("HH:mm:ss") + " domain reloaded\n");
        }

        static void Started(object ctx) => File.WriteAllText(CompilePath, System.DateTime.Now.ToString("HH:mm:ss") + " compile started\n");

        static void Finished(string asm, CompilerMessage[] msgs)
        {
            var b = new StringBuilder();
            int errors = 0;
            foreach (var m in msgs)
            {
                if (m.type == CompilerMessageType.Error) errors++;
                if (m.type == CompilerMessageType.Error || m.message.Contains("warning CS0618"))
                    b.Append(m.type).Append(' ').Append(m.message).Append('\n');
            }
            File.AppendAllText(CompilePath, System.DateTime.Now.ToString("HH:mm:ss") + " " + Path.GetFileName(asm) + ": " + errors + " errors\n" + b);
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Log && !msg.StartsWith("Copper Camp")) return;
            try
            {
                var line = System.DateTime.Now.ToString("HH:mm:ss") + " " + type + " " + msg + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : "") + "\n";
                File.AppendAllText(ConsolePath, line);
            }
            catch { }
        }
    }
}
#endif
