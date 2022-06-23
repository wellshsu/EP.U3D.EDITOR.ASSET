//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using EP.U3D.EDITOR.BASE;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EP.U3D.EDITOR.ASSET
{
    public class BuildTags
    {
        public static Type WorkerType = typeof(BuildTags);
        public static Dictionary<string, string> ScensitiveChar = new Dictionary<string, string>() {
            { "_", "_"},
            { " ", ""},
            { "#", ""},
            { "[", ""},
            { "]", ""}
        };

        [MenuItem(Constants.ASSETS_SET_BUNDLE_TAGS)]
        public static void Invoke1()
        {
            var worker = Activator.CreateInstance(WorkerType) as BuildTags;
            worker.SetSelectTags();
        }

        [MenuItem(Constants.MENU_PATCH_BUILD_DEPS)]
        public static void Invoke2()
        {
            var worker = Activator.CreateInstance(WorkerType) as BuildTags;
            worker.GenAssetDeps();
        }

        [MenuItem(Constants.MENU_PATCH_BUILD_TAG)]
        public static void Invoke3()
        {
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Warning", "Please wait till compile done.", "OK");
                return;
            }
            else
            {
                if (EditorUtility.DisplayDialog("Hint", "Click OK to build assetbundle tag.", "OK", "Cancel"))
                {
                    var worker = Activator.CreateInstance(WorkerType) as BuildTags;
                    worker.RebuileTags();
                }
            }
        }

        public List<string> doneAssets = new List<string>();

        public virtual void RebuileTags()
        {
            doneAssets.Clear();
            SetAllTags();
            ClearAllTags();
            string toast = "Build assets tag done.";
            Helper.Log(toast);
            Helper.ShowToast(toast);
        }

        public virtual void ClearAllTags()
        {
            List<string> assets = new List<string>();
            Helper.CollectAssets(Application.dataPath, assets, ".cs", ".js", ".h", ".lua", ".dll", ".a", ".lib", ".meta", ".tpsheet", ".DS_Store");
            for (int i = 0; i < assets.Count; i++)
            {
                string asset = assets[i];
                if (doneAssets.Contains(asset) == false)
                {
                    AssetImporter assetImporter = AssetImporter.GetAtPath(asset);
                    if (assetImporter && string.IsNullOrEmpty(assetImporter.assetBundleName) == false)
                    {
                        assetImporter.SetAssetBundleNameAndVariant(string.Empty, string.Empty);
                        Helper.Log("clear asset bundle tag of {0}", asset);
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        public virtual void SetAllTags()
        {
            const string assetsDir = "Assets/";
            List<string> sourceAssets = new List<string>();
            Helper.CollectAssets(Constants.BUNDLE_ASSET_WORKSPACE, sourceAssets, ".cs", ".js", ".meta", ".tpsheet", ".DS_Store");
            Helper.CollectAssets(Constants.RESOURCE_WORKSPACE + "/Bundle", sourceAssets, ".cs", ".js", ".meta", ".tpsheet", ".DS_Store");
            for (int i = 0; i < sourceAssets.Count; i++)
            {
                string asset = sourceAssets[i];
                string path = asset;
                doneAssets.Add(asset);
                string temp = asset.Substring(0, asset.LastIndexOf("/"));
                foreach (var item in ScensitiveChar)
                {
                    if (temp.Contains(item.Key)) Helper.LogWarning("invalid char '{0}' in asset path: {1}", item.Key, asset);
                }
                AssetImporter assetImporter = AssetImporter.GetAtPath(asset);
                asset = asset.Substring(assetsDir.Length);
                asset = asset.Substring(0, asset.LastIndexOf("/"));
                asset = asset.Replace("\\", "/").Replace("/", "_");
                foreach (var item in ScensitiveChar)
                {
                    asset = asset.Replace(item.Key, item.Value);
                }
                asset += Constants.ASSET_BUNDLE_FILE_EXTENSION;
                asset = asset.ToLower();
                if (assetImporter)
                {
                    if (assetImporter.assetBundleName != asset) assetImporter.SetAssetBundleNameAndVariant(asset, string.Empty);
                    if (path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".png")) FormatTextureSize(path);
                }
            }
            AssetDatabase.Refresh();

            Dictionary<string, List<string>> dependAssets = Helper.CollectAssetDependency(sourceAssets);
            Dictionary<string, List<string>>.Enumerator ir = dependAssets.GetEnumerator();
            for (int i = 0; i < dependAssets.Count; i++)
            {
                ir.MoveNext();
                KeyValuePair<string, List<string>> kvp = ir.Current;
                List<string> assets = kvp.Value;
                for (int j = 0; j < assets.Count; j++)
                {
                    string asset = assets[j];
                    string path = asset;
                    if (doneAssets.Contains(asset) == false)
                    {
                        doneAssets.Add(asset);
                        if (asset.Contains("Editor/"))
                        {
                            Helper.LogWarning("ignore editor asset deps: {0}", asset);
                        }
                        else
                        {
                            string temp = asset.Substring(0, asset.LastIndexOf("/"));
                            foreach (var item in ScensitiveChar)
                            {
                                if (temp.Contains(item.Key)) Helper.LogWarning("invalid char '{0}' in asset path: {1}", item.Key, asset);
                            }
                            AssetImporter assetImporter = AssetImporter.GetAtPath(asset);
                            asset = asset.Substring(assetsDir.Length);
                            asset = asset.Substring(0, asset.LastIndexOf("/"));
                            asset = asset.Replace("\\", "/").Replace("/", "_");
                            foreach (var item in ScensitiveChar)
                            {
                                asset = asset.Replace(item.Key, item.Value);
                            }
                            asset += Constants.ASSET_BUNDLE_FILE_EXTENSION;
                            asset = asset.ToLower();
                            if (assetImporter)
                            {
                                if (assetImporter.assetBundleName != asset) assetImporter.SetAssetBundleNameAndVariant(asset, string.Empty);
                                if (path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".png")) FormatTextureSize(path);
                            }
                        }
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        public virtual void FormatTextureSize(string asset)
        {
            const string maxTextureSize = "maxTextureSize:";
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(asset);
            if (texture)
            {
                string meta = Helper.StringFormat("{0}.meta", asset);
                if (File.Exists(meta))
                {
                    bool isModified = false;
                    string[] lines = File.ReadAllLines(meta);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (string.IsNullOrEmpty(line) == false)
                        {
                            if (line.Contains(maxTextureSize))
                            {
                                int max = CalcTextureSize(texture);
                                if (texture.width != max || texture.height != max)
                                {
                                    isModified = true;
                                    line = Helper.StringFormat("{0}{1} {2}", line.Substring(0, line.IndexOf(maxTextureSize)), maxTextureSize, max);
                                    lines[i] = line;
                                }
                            }
                        }
                    }
                    if (isModified)
                    {
                        File.Delete(meta);
                        File.WriteAllLines(meta, lines);
                    }
                }
            }
        }

        public virtual int CalcTextureSize(Texture texture)
        {
            if (texture)
            {
                int max = Mathf.Max(texture.width, texture.height);
                if (max <= 32)
                {
                    return 64;
                }
                else if (max <= 64)
                {
                    return 128;
                }
                else if (max <= 128)
                {
                    return 256;
                }
                else if (max <= 256)
                {
                    return 512;
                }
                else if (max <= 512)
                {
                    return 1024;
                }
                else if (max <= 1024)
                {
                    return 2048;
                }
                else if (max <= 2048)
                {
                    return 4096;
                }
                else
                {
                    return 8192;
                }
            }
            return 8192;
        }

        public virtual void GenAssetDeps()
        {
            List<string> sourceAssets = new List<string>();
            Helper.CollectAssets(Constants.BUNDLE_ASSET_WORKSPACE, sourceAssets, ".cs", ".js", ".meta", ".tpsheet", ".DS_Store");
            Helper.CollectAssets(Constants.RESOURCE_WORKSPACE + "/Bundle", sourceAssets, ".cs", ".js", ".meta", ".tpsheet", ".DS_Store");
            Dictionary<string, List<string>> dependAssets = Helper.CollectAssetDependency(sourceAssets);
            Dictionary<string, List<string>>.Enumerator ir = dependAssets.GetEnumerator();
            string destPath = Constants.PROJ_PATH + "Library/AssetDeps.txt";
            if (Helper.HasFile(destPath)) Helper.DeleteFile(destPath);
            using (var destFile = File.Open(destPath, FileMode.Create))
            {
                StreamWriter sw = new StreamWriter(destFile);
                for (int i = 0; i < dependAssets.Count; i++)
                {
                    ir.MoveNext();
                    KeyValuePair<string, List<string>> kvp = ir.Current;
                    sw.WriteLine(kvp.Key);
                    for (int j = 0; j < kvp.Value.Count; j++)
                    {
                        string asset = kvp.Value[j];
                        if (asset.Contains("Editor/") == false && asset != kvp.Key) sw.WriteLine("\t" + asset);
                    }
                }
            }
            string toast = "Build assets deps done.";
            Helper.Log("[FILE@{0}] {1}", destPath, toast);
        }

        public virtual void SetSelectTags()
        {
            UnityEngine.Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                UnityEngine.Object asset = selected[i];
                if (asset)
                {
                    string assetPath = AssetDatabase.GetAssetPath(asset);
                    string bundleName = assetPath.Substring("Assets/".Length);
                    bundleName = bundleName.Substring(0, bundleName.LastIndexOf("/"));
                    foreach (var item in ScensitiveChar)
                    {
                        if (bundleName.Contains(item.Key)) Helper.LogWarning("invalid char '{0}' in asset path: {1}", item.Key, asset);
                    }
                    bundleName = bundleName.Replace("/", "_");
                    foreach (var item in ScensitiveChar)
                    {
                        bundleName = bundleName.Replace(item.Key, item.Value);
                    }
                    bundleName = bundleName.ToLower();
                    bundleName = bundleName + Constants.ASSET_BUNDLE_FILE_EXTENSION;
                    AssetImporter assetImporter = AssetImporter.GetAtPath(assetPath);
                    if (assetImporter)
                    {
                        assetImporter.SetAssetBundleNameAndVariant(bundleName, string.Empty);
                    }
                }
            }
        }
    }
}