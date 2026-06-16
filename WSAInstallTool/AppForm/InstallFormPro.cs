using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WSAInstallTool.AppForm;
using WSAInstallTool.Util;

namespace WSAInstallTool
{
    public partial class InstallFormPro : Form
    {
        private String[] args = null;

        // 额外的设备命令
        private string extraCommand = "";

        private string apkPath = "";

        // 完整的权限列表
        private List<string> permissionList;

        private AAPTParseUtil aaptParseUtil;

        // Cmd 回调委托
        private delegate void CmdCallbackDelegate(string result);

        // 检测apk是否存在风险的委托
        private delegate void BadApkDelegate(bool isBad);

        public InstallFormPro()
        {
            InitializeComponent();
        }

        public InstallFormPro(String[] args)
        {
            this.args = args;
            InitializeComponent();
        }

        private BundleFormatUtil bundleUtil;

        private void InstallFormPro_Load(object sender, EventArgs e)
        {
            InitAdbServer();
            InitLanguage();
            Console.WriteLine("dir == " + Environment.CurrentDirectory);

            if (args != null && args.Length > 0)
            {
                apkPath = args[0];
            }

            // Check if it's a bundle format (xapk/apks)
            if (BundleFormatUtil.IsBundleFormat(apkPath))
            {
                bundleUtil = new BundleFormatUtil(apkPath);
                if (!bundleUtil.Extract())
                {
                    MessageBox.Show("Failed to extract bundle file: " + Path.GetFileName(apkPath));
                    this.Close();
                    return;
                }
                // Use the base APK for parsing
                apkPath = bundleUtil.BaseApkPath;
            }

            string result = CMDUtil.ExecCMD("aapt.exe", "dump badging \"" + apkPath + "\"");

            aaptParseUtil = new AAPTParseUtil(result);

            packageNameLabel.Text = LangUtil.Instance.GetPackageName() +
                (string.IsNullOrEmpty(aaptParseUtil.GetPackageName()) ? LangUtil.Instance.GetAppUnknown() : aaptParseUtil.GetPackageName());
            versionNameLabel.Text = LangUtil.Instance.GetVersionName() +
                (string.IsNullOrEmpty(aaptParseUtil.GetVersionName()) ? LangUtil.Instance.GetAppUnknown() : aaptParseUtil.GetVersionName()); ;
            minVersionLabel.Text = LangUtil.Instance.GetMinVersionName() + aaptParseUtil.getMinSupportVersion();

            BadApkDelegate badApkDelegate = CheckApkSafetyComplete;
            Thread ts = new Thread(CheckApkSafety);
            ts.Start(badApkDelegate);

            StringBuilder permissionStringBuilder = new StringBuilder();
            permissionList = aaptParseUtil.GetPermissionDetailList();
            if (permissionList.Count > 10)
            {
                foreach (string s in permissionList.GetRange(0, 10))
                {
                    permissionStringBuilder.Append("· ").Append(s).Append("\n");
                }
                permissionStringBuilder.Append("  ......");
                moreLinkLabel.Visible = true;
            }
            else
            {
                foreach (string s in permissionList)
                {
                    permissionStringBuilder.Append("· ").Append(s).Append("\n");
                }
                moreLinkLabel.Visible = false;
            }
            permissionLabel.Text = LangUtil.Instance.GetPersimissions() + "\n"
                + (string.IsNullOrEmpty(permissionStringBuilder.ToString().Trim()) ? LangUtil.Instance.GetNothing() : permissionStringBuilder.ToString().Trim());

            appNameLabel.Text = string.IsNullOrEmpty(aaptParseUtil.GetAppName()) ? LangUtil.Instance.GetAppUnknown() : aaptParseUtil.GetAppName();

            spaceLabel.Text = LangUtil.Instance.GetSize() + GetApkSpace();

            try
            {
                string iconPath = aaptParseUtil.GetApkIcon(apkPath);
                Debug.WriteLine("load logo path => " + iconPath);
                iconPictureBox.Load(iconPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("load logo error => " + ex.Message);
            }

            ThreadPool.QueueUserWorkItem(LoadDevices);
        }

        private void InitAdbServer()
        {
            ThreadPool.QueueUserWorkItem(CMDUtil.StartAdbServer);
        }

        private void LoadDevices(object state)
        {
            try
            {
                Thread.Sleep(1000);
                string cmdRunResult = CMDUtil.ExecCMD(CommonUtil.GetAdbPath(), "devices").Trim();
                string[] splitCmdRunResult = Regex.Split(cmdRunResult, "List of devices attached");
                if (splitCmdRunResult.Length != 2)
                {
                    this.Invoke(new MethodInvoker(delegate()
                    {
                        deviceLabel.Text = "未检测到设备";
                        deviceComboBox.Enabled = false;
                        installButton.Enabled = false;
                    }));
                    return;
                }
                string deviceResult = splitCmdRunResult[1].Trim();
                if (string.IsNullOrEmpty(deviceResult))
                {
                    this.Invoke(new MethodInvoker(delegate()
                    {
                        deviceLabel.Text = "未检测到设备";
                        deviceComboBox.Enabled = false;
                        installButton.Enabled = false;
                    }));
                    return;
                }
                string[] devices = deviceResult.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> devicesList = new List<string>();
                foreach (string device in devices)
                {
                    string[] deviceNames = device.Split('\t');
                    if (deviceNames.Length == 2)
                    {
                        devicesList.Add(deviceNames[0]);
                    }
                }
                this.Invoke(new MethodInvoker(delegate()
                {
                    deviceComboBox.Items.Clear();
                    if (devicesList.Count == 0)
                    {
                        deviceLabel.Text = "未检测到设备";
                        deviceComboBox.Enabled = false;
                        installButton.Enabled = false;
                    }
                    else
                    {
                        foreach (string d in devicesList)
                        {
                            deviceComboBox.Items.Add(d);
                        }
                        deviceComboBox.SelectedIndex = 0;
                        deviceComboBox.Enabled = true;
                        installButton.Enabled = true;
                        deviceLabel.Text = "选择设备:";
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[InstallFormPro][LoadDevices] error => " + ex.Message);
                this.Invoke(new MethodInvoker(delegate()
                {
                    deviceLabel.Text = "设备加载失败";
                    deviceComboBox.Enabled = false;
                    installButton.Enabled = false;
                }));
            }
        }


        /* ************************************检测Apk状态 START*************************************************/
        /// <summary>
        /// 检测Apk是否为正常
        /// </summary>
        /// <returns></returns>
        private void CheckApkSafety(object obj)
        {
            // 根据配置是否开启apk检测
            if (!PreferenceUtil.Instance.GetScanBadApk())
            {
                return;
            }
            BadApkDelegate callback = obj as BadApkDelegate;
            try
            {
                bool isBad = CommonUtil.IsBadApk(aaptParseUtil.GetPackageName(), HashUtil.GetSha256Hash(apkPath));
                Console.WriteLine("[InstallFormPro][CheckApkSafety] isBad = " + isBad);
                callback(isBad);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[InstallFormPro][CheckApkSafety] error => " + ex.Message);
                callback(false);
            }
        }

        /// <summary>
        /// Apk状态检测完成
        /// </summary>
        /// <param name="isBadApk"></param>
        private void CheckApkSafetyComplete(bool isBadApk)
        {
            if (this.IsDisposed)
            {
                return;
            }
            this.Invoke(new MethodInvoker(delegate()
            {
                badApkPictureBox.Visible = true;
                if (isBadApk)
                {
                    badApkPictureBox.Image = Properties.Resources.bad_apk;
                }
                else
                {
                    badApkPictureBox.Image = Properties.Resources.bad_apk_safety;
                }
            }));
        }

        /* ************************************检测Apk状态 E N D*************************************************/



        /// <summary>
        /// 获取APK大小
        /// 如果文件大小是0-1024B 以内的   显示以B为单位
        /// 如果文件大小是1KB-1024KB之间的 显示以KB为单位
        /// 如果文件大小是1M-1024M之间的   显示以M为单位
        /// 如果文件大小是1024M以上的      显示以GB为单位
        /// <returns></returns>
        private string GetApkSpace()
        {
            string result = LangUtil.Instance.GetAppUnknown();
            FileStream file = null;
            try
            {
                file = File.Open(apkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                long space = file.Length;

                if (space < 1024)
                    result = string.Format("{0:F}", space) + " B";
                else if (space > 1024 && space <= Math.Pow(1024, 2))
                    result = string.Format("{0:F}", (space / 1024.0)) + " KB";
                else if (space > Math.Pow(1024, 2) && space <= Math.Pow(1024, 3))
                    result = string.Format("{0:F}", (space / 1024.0 / 1024.0)) + " MB";
                else
                    result = string.Format("{0:F}", (space / 1024.0 / 1024.0 / 1024.0)) + " GB";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[GetApkSpace Error] " + ex.Message);
            }
            finally
            {
                if (file != null)
                {
                    file.Close();
                }
            }

            return result;
        }

        /// <summary>
        /// 安装点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void installButton_Click(object sender, EventArgs e)
        {
            if (deviceComboBox.SelectedItem == null)
            {
                MessageBox.Show("请先选择设备");
                return;
            }

            string selectedDevice = deviceComboBox.SelectedItem.ToString();
            extraCommand = string.IsNullOrEmpty(selectedDevice) ? "" : "-s " + selectedDevice + " ";

            installButton.Enabled = false;
            installButton.Text = LangUtil.Instance.GetAppInstalling();
            deviceComboBox.Visible = false;
            deviceLabel.Visible = false;
            installProgressBar.Visible = true;

            CmdCallbackDelegate installCallback = InstallApkComplete;
            Thread thread = new Thread(InstallApkCMD);
            thread.Start(installCallback);
        }

        /// <summary>
        /// 执行安装APK的命令
        /// </summary>
        /// <param name="obj"></param>
        private void InstallApkCMD(object obj)
        {
            CmdCallbackDelegate callback = obj as CmdCallbackDelegate;
            string installCommand = PreferenceUtil.Instance.GetInstallMethodCommand();
            string result;

            if (bundleUtil != null)
            {
                // Use bundle install command (install-multiple for OBB/splits)
                result = CMDUtil.ExecCMD(CommonUtil.GetAdbPath(), extraCommand + bundleUtil.GetInstallCommand());
            }
            else
            {
                result = CMDUtil.ExecCMD(CommonUtil.GetAdbPath(), extraCommand + "install " + installCommand + " \"" + apkPath + "\"");
            }

            Debug.WriteLine("[InstallFormPro][InstallApkCMD] command => " + result);
            callback(result);
        }

        ///// <summary>
        ///// 执行安装APK的命令
        ///// </summary>
        ///// <param name="obj"></param>
        //private void InstallApkCMDWithDevice(object obj)
        //{
        //    CmdCallbackDelegate callback = obj as CmdCallbackDelegate;
        //    string result = CMDUtil.ExecCMD("adb.exe", "install device" + apkPath);
        //    callback(result);
        //}

        /// <summary>
        /// 安装完成
        /// </summary>
        /// <param name="result"></param>
        private void InstallApkComplete(string result)
        {

            this.Invoke(new MethodInvoker(delegate()
            {
                installButton.Enabled = true;
                installButton.Text = LangUtil.Instance.GetAppInstall();
                installProgressBar.Visible = false;
                deviceComboBox.Visible = true;
                deviceLabel.Visible = true;

                if (!string.IsNullOrEmpty(result) && result.Replace("Performing Streamed Install", "").Trim() == "Success")
                {
                    if (PreferenceUtil.Instance.IsCloseAfterInstalled())
                    {
                        // 关闭窗口     
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show(LangUtil.Instance.GetAppInstallSuccess());
                    }
                }
                else if (!string.IsNullOrEmpty(result) && result.Contains("[INSTALL_FAILED_VERSION_DOWNGRADE]"))
                {
                    MessageBox.Show(LangUtil.Instance.GetAppInstallFailedDowngrade());
                }
                else
                {
                    MessageBox.Show(LangUtil.Instance.GetAppInstallFailed() + result);
                }
            }));
        }

        /// <summary>
        /// 查看更多点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void moreLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PermissionForm pf = new PermissionForm(permissionList);
            pf.Show();
        }

        /// <summary>
        /// 点击设置按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void settingButton_Click(object sender, EventArgs e)
        {
            new SettingForm().Show();
        }

        /// <summary>
        /// 初始化语言
        /// </summary>
        private void InitLanguage()
        {
            this.Text = LangUtil.Instance.GetInstallFromTitle();
            moreLinkLabel.Text = LangUtil.Instance.GetViewMorePermissions();
            installButton.Text = LangUtil.Instance.GetAppInstall();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            bundleUtil?.Cleanup();
        }
    }
}
