using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WSAInstallTool.Util
{
    /// <summary>
    /// XAPK/APKS bundle format handler
    /// </summary>
    class BundleFormatUtil
    {
        private string _bundlePath;
        private string _tempDir;
        private string _baseApkPath;
        private List<string> _splitApkPaths;
        private List<string> _obbPaths;
        private string _packageName;
        private string _versionName;

        public string BaseApkPath => _baseApkPath;
        public List<string> SplitApkPaths => _splitApkPaths;
        public List<string> ObbPaths => _obbPaths;
        public string PackageName => _packageName;
        public string VersionName => _versionName;

        public BundleFormatUtil(string bundlePath)
        {
            _bundlePath = bundlePath;
            _splitApkPaths = new List<string>();
            _obbPaths = new List<string>();
        }

        /// <summary>
        /// Check if the file is a supported bundle format
        /// </summary>
        public static bool IsBundleFormat(string filePath)
        {
            string ext = Path.GetExtension(filePath)?.ToLower();
            return ext == ".xapk" || ext == ".apks";
        }

        /// <summary>
        /// Extract bundle and locate base APK
        /// </summary>
        public bool Extract()
        {
            try
            {
                _tempDir = Path.Combine(Path.GetTempPath(), "ApkInstaller_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_tempDir);

                Debug.WriteLine("[BundleFormatUtil][Extract] extracting " + _bundlePath + " to " + _tempDir);
                ZipFile.ExtractToDirectory(_bundlePath, _tempDir);

                // Parse manifest.json if exists
                ParseManifest();

                // Find base APK
                FindApks();

                if (string.IsNullOrEmpty(_baseApkPath))
                {
                    Debug.WriteLine("[BundleFormatUtil][Extract] no base APK found");
                    return false;
                }

                Debug.WriteLine("[BundleFormatUtil][Extract] base APK: " + _baseApkPath);
                Debug.WriteLine("[BundleFormatUtil][Extract] split APKs: " + _splitApkPaths.Count);
                Debug.WriteLine("[BundleFormatUtil][Extract] OBBs: " + _obbPaths.Count);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BundleFormatUtil][Extract] error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Get the install command for adb
        /// </summary>
        public string GetInstallCommand()
        {
            if (_obbPaths.Count > 0 || _splitApkPaths.Count > 0)
            {
                // Use install-multiple for bundles with OBB or split APKs
                List<string> allFiles = new List<string>();
                allFiles.Add(_baseApkPath);
                allFiles.AddRange(_splitApkPaths);
                allFiles.AddRange(_obbPaths);

                StringBuilder sb = new StringBuilder("install-multiple");
                foreach (string file in allFiles)
                {
                    sb.Append(" \"").Append(file).Append("\"");
                }
                return sb.ToString();
            }
            else
            {
                // Simple install for base APK only
                return "install \"" + _baseApkPath + "\"";
            }
        }

        /// <summary>
        /// Clean up temp files
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                    Debug.WriteLine("[BundleFormatUtil][Cleanup] deleted " + _tempDir);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BundleFormatUtil][Cleanup] error: " + ex.Message);
            }
        }

        private void ParseManifest()
        {
            string manifestPath = Path.Combine(_tempDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Debug.WriteLine("[BundleFormatUtil][ParseManifest] manifest.json not found");
                return;
            }

            try
            {
                string json = File.ReadAllText(manifestPath, Encoding.UTF8);
                Debug.WriteLine("[BundleFormatUtil][ParseManifest] manifest content: " + json);

                // Simple JSON parsing without external dependencies
                // Extract package name
                Match pkgMatch = Regex.Match(json, @"""package_name""\s*:\s*""([^""]+)""");
                if (pkgMatch.Success)
                {
                    _packageName = pkgMatch.Groups[1].Value;
                    Debug.WriteLine("[BundleFormatUtil][ParseManifest] package: " + _packageName);
                }

                // Extract version name
                Match verMatch = Regex.Match(json, @"""version_name""\s*:\s*""([^""]+)""");
                if (verMatch.Success)
                {
                    _versionName = verMatch.Groups[1].Value;
                    Debug.WriteLine("[BundleFormatUtil][ParseManifest] version: " + _versionName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BundleFormatUtil][ParseManifest] error: " + ex.Message);
            }
        }

        private void FindApks()
        {
            string[] apkFiles = Directory.GetFiles(_tempDir, "*.apk", SearchOption.AllDirectories);
            Debug.WriteLine("[BundleFormatUtil][FindApks] found " + apkFiles.Length + " APK files");

            if (apkFiles.Length == 0) return;

            // If we have package name from manifest, try to find the matching APK
            if (!string.IsNullOrEmpty(_packageName))
            {
                foreach (string apk in apkFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(apk);
                    if (fileName == _packageName || fileName.Replace(".", "_") == _packageName.Replace(".", "_"))
                    {
                        _baseApkPath = apk;
                        Debug.WriteLine("[BundleFormatUtil][FindApks] base APK matched by package name: " + apk);
                        break;
                    }
                }
            }

            // Fallback: use the first APK as base
            if (string.IsNullOrEmpty(_baseApkPath))
            {
                _baseApkPath = apkFiles[0];
                Debug.WriteLine("[BundleFormatUtil][FindApks] using first APK as base: " + _baseApkPath);
            }

            // Collect split APKs and OBBs
            foreach (string apk in apkFiles)
            {
                if (apk == _baseApkPath) continue;

                string fileName = Path.GetFileName(apk);
                // Split APKs typically start with "split_"
                if (fileName.StartsWith("split_", StringComparison.OrdinalIgnoreCase))
                {
                    _splitApkPaths.Add(apk);
                }
                else
                {
                    _splitApkPaths.Add(apk);
                }
            }

            // Find OBB files
            string[] obbFiles = Directory.GetFiles(_tempDir, "*.obb", SearchOption.AllDirectories);
            _obbPaths.AddRange(obbFiles);
        }
    }
}
