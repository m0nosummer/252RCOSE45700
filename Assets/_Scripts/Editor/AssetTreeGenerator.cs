using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class AssetTreeGenerator
{
    [MenuItem("EditorScript/Copy '_Scripts' Folder Tree to Clipboard")]
    private static void CopyAssetTreeToClipboard()
    {
        string targetFolderName = "_Scripts";
        string rootPath = Path.Combine(Application.dataPath, targetFolderName);

        if (!Directory.Exists(rootPath))
        {
            Debug.LogError($"Error: Assets 폴더 안에 '{targetFolderName}' 폴더를 찾을 수 없습니다!");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(targetFolderName);

        ProcessDirectory(rootPath, "", sb, true);

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"'{targetFolderName}' folder tree has been copied to the clipboard!");
    }

    private static void ProcessDirectory(string path, string prefix, StringBuilder sb, bool isRoot)
    {
        var subdirectories = Directory.GetDirectories(path);
        var files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
        var filteredFiles = System.Array.FindAll(files, file => !file.EndsWith(".meta"));

        var allEntries = new string[subdirectories.Length + filteredFiles.Length];
        subdirectories.CopyTo(allEntries, 0);
        filteredFiles.CopyTo(allEntries, subdirectories.Length);

        for (int i = 0; i < allEntries.Length; i++)
        {
            var entry = allEntries[i];
            bool isLast = (i == allEntries.Length - 1);
            bool isDirectory = Directory.Exists(entry);
            
            string currentPrefix = isLast ? "└── " : "├── ";
            sb.Append(prefix).Append(currentPrefix).AppendLine(Path.GetFileName(entry));

            if (isDirectory)
            {
                string nextPrefix = isLast ? "    " : "│   ";
                ProcessDirectory(entry, prefix + nextPrefix, sb, false);
            }
        }
    }
}