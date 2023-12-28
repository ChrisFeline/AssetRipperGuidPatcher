#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;

namespace Kittenji.Tools
{
    using GuidList = System.Collections.Generic.Dictionary<string, GuidSwap>;

    public class AssetRipperGuidPatch : EditorWindow
    {
        static readonly GuidList ScriptGuidSwap = new GuidList();
        static readonly GuidList ShaderGuidSwap = new GuidList();

        // Scripts
        static readonly Regex GuidPattern = new Regex(@"guid:\s(?<guid>[0-9A-Za-z]+)", RegexOptions.Compiled);
        // Shaders
        static readonly Regex ShaderPropPattern = new Regex(@"  m_Shader: {fileID: (?<file>\d+), guid: (?<guid>[0-9A-f-a-f]+), type: (?<type>\d+)}", RegexOptions.Compiled);
        static readonly Regex ShaderNamePattern = new Regex(@"Shader\s+""(?<name>.*)""[\s\S\r]*?{", RegexOptions.Compiled);

        static ExportedProject ExportedProj;

        const string LastExpPathKey = "LastExportedPath";
        [MenuItem("Kittenji/AssetRipper Guid Patch")]
        static void Init()
        {
            string lastpath = EditorPrefs.GetString(LastExpPathKey);
            if (string.IsNullOrEmpty(lastpath) || !Directory.Exists(lastpath))
                lastpath = "/";

            string exportedPath = EditorUtility.OpenFolderPanel("Open Exported Project", lastpath, "ExportedProject");
            if (string.IsNullOrEmpty(exportedPath))
                return;

            EditorPrefs.SetString(LastExpPathKey, Path.GetDirectoryName(exportedPath));

            exportedPath = Path.Combine(exportedPath, "Assets");
            if (!Directory.Exists(exportedPath))
            {
                EditorUtility.DisplayDialog("Error", "Assets folder not found.", "Ok");
                return;
            }

            ExportedProj = new ExportedProject(exportedPath);

            StartFix();
        }

        private static void StartFix()
        {
            ScriptGuidSwap.Clear();
            ShaderGuidSwap.Clear();

            if (!string.IsNullOrEmpty(ExportedProj.ScriptsPath))
            {
                string[] monoScripts = AssetDatabase.FindAssets("t:MonoScript");

                for (int i = 0; i < monoScripts.Length; i++)
                {
                    string scriptGuid = monoScripts[i];
                    EditorUtility.DisplayProgressBar("Indexing MonoScript", scriptGuid, i / (monoScripts.Length - 1f));

                    string assetPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                    System.Type classType = monoScript.GetClass();
                    if (classType == null || !classType.IsSubclassOf(typeof(Component))) continue;

                    string assemblyName = classType.Assembly.GetName().Name;
                    string fullName = classType.FullName.Replace('.', '/');
                    string className = classType.Name;

                    string path = Path.Combine(ExportedProj.ScriptsPath, assemblyName, fullName).Replace('\\', '/');
                    string sourceFile = path + ".cs";
                    string metaFile = sourceFile + ".meta";

                    if (!File.Exists(metaFile)) continue;

                    string metaContent = File.ReadAllText(metaFile);

                    Match match = GuidPattern.Match(metaContent);
                    if (!match.Success)
                    {
                        Debug.LogError("Error matching .meta file GUID\n" + metaFile);
                        continue;
                    }

                    string guid = match.Groups["guid"].Value;
                    if (string.IsNullOrEmpty(guid) || guid == scriptGuid) continue;

                    Debug.Log(sourceFile + " " + File.Exists(metaFile));
                    ScriptGuidSwap.Add(guid, new GuidSwap()
                    {
                        Name = className,
                        Guid = scriptGuid,
                        File = sourceFile
                    });
                }
            }

            if (!string.IsNullOrEmpty(ExportedProj.ShaderPath))
            {
                string[] files = Directory.GetFiles(ExportedProj.ShaderPath);
                
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    if (!file.EndsWith(".shader")) continue;
                    EditorUtility.DisplayProgressBar("Indexing Shaders", file, i / (files.Length - 1f));

                    string content = File.ReadAllText(file);
                    string meta = File.ReadAllText(file + ".meta");

                    Match match = GuidPattern.Match(meta);
                    if (!match.Success) continue;
                    string guid = match.Groups["guid"].Value;

                    match = ShaderNamePattern.Match(content);
                    if (!match.Success) continue;
                    string name = match.Groups["name"].Value;

                    Shader find = Shader.Find(name);
                    if (find == null)
                    {
                        Debug.LogWarning("Could not find shader with name: " + name);
                        continue;
                    }

                    var globalId = GlobalObjectId.GetGlobalObjectIdSlow(find);
                    string assetGuid = globalId.assetGUID.ToString();
                    ulong objectId = globalId.targetObjectId;

                    Debug.Log($"{guid} | {name} || {find.name} | {assetGuid} {objectId}");

                    ShaderGuidSwap.Add(guid, new GuidSwap()
                    {
                        Name = name,
                        Guid = assetGuid,
                        File = objectId.ToString()
                    });
                }
            }

            InstanceReplaceCount = 0;
            ShaderReplaceCount = 0;
            TraverseFiles(ExportedProj.Location);
            EditorUtility.ClearProgressBar();

            int totalReplacements = InstanceReplaceCount + ShaderReplaceCount;
            if (totalReplacements == 0) {
                EditorUtility.DisplayDialog("Replacement Results", "Nothing was replaced.", "Ok");
                return;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine($"Replaced '{totalReplacements}' assets in exported project.");

            if (InstanceReplaceCount > 0)
                report.AppendLine("Instances: " + InstanceReplaceCount);

            if (ShaderReplaceCount > 0)
                report.AppendLine("Shaders: " + ShaderReplaceCount);
            
            EditorUtility.DisplayDialog("Replacement Results", report.ToString(), "Ok");
        }

        static void TraverseFiles(string dir)
        {
            string[] files = Directory.GetFiles(dir);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string ext = Path.GetExtension(file).ToLowerInvariant();
                EditorUtility.DisplayProgressBar("Traversing", dir + '\n' + file, i / (files.Length - 1f));

                switch (ext)
                {
                    case ".prefab":
                    case ".unity":
                        DoInstanceReplacement(file);
                        break;

                    case ".material":
                    case ".mat":
                        DoShaderReplacement(file);
                        break;

                    default:
                        break;
                }
            }

            files = Directory.GetDirectories(dir);
            if (files.Length > 0)
                foreach (string d in files) TraverseFiles(d);
        }

        static int InstanceReplaceCount = 0;
        static void DoInstanceReplacement(string file)
        {
            if (ScriptGuidSwap.Count == 0) return;

            string content = File.ReadAllText(file);

            int r_count = 0; // Replacement counts
            content = GuidPattern.Replace(content, m =>
            {
                string guid = m.Groups["guid"].Value;

                if (ScriptGuidSwap.ContainsKey(guid))
                {
                    r_count++;
                    return "guid: " + ScriptGuidSwap[guid].Guid;
                }
                else return m.Value;
            });

            if (r_count > 0)
            {
                Debug.Log($"Replaced '{r_count}' In: {Path.GetFileName(file)}");
                File.WriteAllText(file, content);

                InstanceReplaceCount++;
            }
        }

        static int ShaderReplaceCount = 0;
        static void DoShaderReplacement(string file)
        {
            if (ShaderGuidSwap.Count == 0) return;

            string content = File.ReadAllText(file);

            int r_count = 0; // Replacement counts
            content = ShaderPropPattern.Replace(content, m =>
            {
                string guid = m.Groups["guid"].Value;
                string file = m.Groups["file"].Value;
                string type = m.Groups["type"].Value;

                if (ShaderGuidSwap.ContainsKey(guid))
                {
                    r_count++;
                    GuidSwap swap = ShaderGuidSwap[guid];
                    return "  m_Shader: {fileID: " + swap.File + ", guid: "+ swap.Guid + ", type: "+ type + "}";
                }
                else return m.Value;
            });

            if (r_count > 0)
            {
                Debug.Log($"Replaced '{r_count}' In: {Path.GetFileName(file)}");
                File.WriteAllText(file, content);

                ShaderReplaceCount++;
            }
        }
    }

    public struct GuidSwap
    {
        public string File;
        public string Name;
        public string Guid;
    }

    public struct ExportedProject
    {
        public string Location;

        public string ScriptsPath;
        public string ShaderPath;

        public ExportedProject(string path)
        {
            Location = path;

            ScriptsPath = ValidatePath(path, "Scripts");
            ShaderPath = ValidatePath(path, "Shader");
        }

        private static string ValidatePath(string path, string name)
        {
            path = Path.Combine(path, name);
            return Directory.Exists(path) ? path : null;
        }
    }
}
#endif