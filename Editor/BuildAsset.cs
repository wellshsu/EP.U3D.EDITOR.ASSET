//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using EP.U3D.EDITOR.BASE;
using EP.U3D.LIBRARY.ASSET;

namespace EP.U3D.EDITOR.ASSET
{
    public class BuildAsset
    {
        public static Type WorkerType = typeof(BuildAsset);

        [MenuItem(Constants.MENU_PATCH_BUILD_ASSET)]
        public static void Invoke1()
        {
            var worker = Activator.CreateInstance(WorkerType) as BuildAsset;
            worker.BuildAll();
        }

        [MenuItem(Constants.MENU_PATCH_BUILD_SCENE)]
        public static void Invoke2()
        {
            Helper.CollectScenes();
        }

        public FileManifest preBuildManifest;
        public FileManifest postBuildManifest;
        public AssetBundleManifest manifest;

        public virtual void BuildAll()
        {
            Caching.ClearCache();
            string temp = Path.Combine(Constants.BUILD_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE);
            if (File.Exists(temp)) Helper.CopyFile(temp, temp + ".last");
            PrepareDirectory();
            ProcessBuild();
            GenerateManifest();
            CaculateDiffer();
        }

        public virtual void PrepareDirectory()
        {
            if (Directory.Exists(Constants.BUILD_ASSET_BUNDLE_PATH) == false)
            {
                Directory.CreateDirectory(Constants.BUILD_ASSET_BUNDLE_PATH);
            }
        }

        public virtual void ProcessBuild()
        {
            BuildPipeline.BuildAssetBundles(Constants.BUILD_ASSET_BUNDLE_PATH, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
        }

        public virtual void GenerateManifest()
        {
            string abManifestFilePath = Constants.BUILD_ASSET_BUNDLE_PATH + Constants.ASSET_BUNDLE_MANIFEST_FILE;
            string manifestFilePath = Constants.BUILD_ASSET_BUNDLE_PATH + Constants.ASSET_BUNDLE_MANIFEST_FILE + ".manifest";
            string assetManifestFilePath = Constants.BUILD_ASSET_BUNDLE_PATH + Constants.MANIFEST_FILE;
            if (File.Exists(assetManifestFilePath))
            {
                File.Delete(assetManifestFilePath);
            }
            if (File.Exists(abManifestFilePath) == false)
            {
                Helper.LogError("error caused by null ab manifest.");
                return;
            }
            FileStream fs = new FileStream(assetManifestFilePath, FileMode.OpenOrCreate);
            StreamWriter sw = new StreamWriter(fs);
            // write ab manifest file;
            string manifestMD5 = Helper.FileMD5(abManifestFilePath);
            int manifestSize = Helper.FileSize(abManifestFilePath);
            sw.WriteLine(Constants.ASSET_BUNDLE_MANIFEST_FILE + "|" + manifestMD5 + "|" + manifestSize);
            string[] lines = File.ReadAllLines(manifestFilePath);
            List<string> abs = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("      Name: "))
                {
                    line = line.Replace("      Name: ", "");
                    line = line.Trim();
                    abs.Add(line);
                }
            }
            AssetBundle bundle = AssetBundle.LoadFromFile(Constants.BUILD_ASSET_BUNDLE_PATH + "Assets");
            manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            List<string> abs2 = new List<string>();
            int count = 0;
            while (abs.Count > 0)
            {
                for (int i = 0; i < abs.Count;)
                {
                    string ab = abs[i];
                    string[] deps = manifest.GetAllDependencies(ab);
                    if (deps.Length == count)
                    {
                        abs.RemoveAt(i);
                        abs2.Add(ab);
                    }
                    else
                    {
                        i++;
                    }
                }
                count++;
            }
            for (int i = 0; i < abs2.Count; i++)
            {
                string ab = abs2[i];
                string filePath = Constants.BUILD_ASSET_BUNDLE_PATH + ab;
                int size = Helper.FileSize(filePath);
                string md5 = Helper.FileMD5(filePath);
                sw.WriteLine(ab + "|" + md5 + "|" + size);
            }
            sw.Close();
            fs.Close();
            bundle.Unload(true);
        }

        public virtual void CaculateDiffer()
        {
            preBuildManifest = new FileManifest(Constants.BUILD_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE + ".last");
            preBuildManifest.Initialize();
            Helper.DeleteFile(Path.Combine(Constants.BUILD_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE + ".last"));
            postBuildManifest = new FileManifest(Constants.BUILD_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE);
            postBuildManifest.Initialize();
            FileManifest.DifferInfo differInfo = preBuildManifest.CompareWith(postBuildManifest);
            string toast = string.Empty;
            for (int i = 0; i < differInfo.Modified.Count; i++)
            {
                FileManifest.FileInfo fileInfo = differInfo.Modified[i];
                Helper.Log("[{0}] has been modified.", fileInfo.Name);
            }
            if (differInfo.Modified.Count > 0)
            {
                toast += string.Format("{0} asset(s) has been modified.\n", differInfo.Modified.Count);
            }
            for (int i = 0; i < differInfo.Added.Count; i++)
            {
                FileManifest.FileInfo fileInfo = differInfo.Added[i];
                Helper.Log("[{0}] has been added.", fileInfo.Name);
            }
            if (differInfo.Added.Count > 0)
            {
                toast += string.Format("{0} asset(s) has been added.\n", differInfo.Added.Count);
            }
            for (int i = 0; i < differInfo.Deleted.Count; i++)
            {
                FileManifest.FileInfo fileInfo = differInfo.Deleted[i];
                string filePath1 = Constants.BUILD_ASSET_BUNDLE_PATH + fileInfo.Name;
                string filePath2 = Constants.BUILD_ASSET_BUNDLE_PATH + fileInfo.Name + ".manifest";
                Helper.Log("[{0}] has been deleted.", fileInfo.Name);
                if (File.Exists(filePath1))
                {
                    File.Delete(filePath1);
                }
                if (File.Exists(filePath2))
                {
                    File.Delete(filePath2);
                }
            }
            if (differInfo.Deleted.Count > 0)
            {
                toast += string.Format("{0} asset(s) has been deleted.\n", differInfo.Deleted.Count);
            }
            if (differInfo.HasDiffer == false)
            {
                Helper.Log("No asset to build.");
                toast = "No asset to build.";
            }
            postBuildManifest.ToFile();
            Helper.ShowToast(toast);
        }
    }
}
