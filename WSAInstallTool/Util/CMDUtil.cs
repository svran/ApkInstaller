using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace WSAInstallTool
{
    class CMDUtil
    {
        private static string AdbPath { get { return Util.CommonUtil.GetAdbPath(); } }
        //执行命令行命令,获取输出结果
        //cmd_filename: DOS程序文件名
        //arguments: 命令行参数
        public static string ExecCMD(string cmd_filename, string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo(cmd_filename, arguments);//如：调用ipconfig ,并传入参数: /all 
            psi.CreateNoWindow = true; //若为false，则会出现cmd的黑窗体 
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.StandardOutputEncoding = Encoding.UTF8;
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
            string s = p.StandardOutput.ReadToEnd();
            s += p.StandardError.ReadToEnd();
            return s;
        }

        /// <summary>
        /// 执行bat文件
        /// </summary>
        /// <param name="batName"></param>
        public static void ExecBat(string batName)
        {
            var pStartInfo = new System.Diagnostics.ProcessStartInfo(batName);
            pStartInfo.CreateNoWindow = false;
            pStartInfo.UseShellExecute = true;
            pStartInfo.RedirectStandardError = false;
            pStartInfo.RedirectStandardInput = false;
            pStartInfo.RedirectStandardOutput = false;
            Process p = Process.Start(pStartInfo);
            p.WaitForExit();
        }

        /// <summary>
        /// 开启Adb服务
        /// </summary>
        public static void StartAdbServer(object state)
        {
            Console.WriteLine("[CMDUtil][StartAdbServer] starting adb server...");
            string result = CMDUtil.ExecCMD(AdbPath, "start-server");
            Console.WriteLine("[CMDUtil][StartAdbServer] adb server result: " + result);
        }

        public static void StopAdbServer(object state)
        {
            Console.WriteLine("[CMDUtil][StopAdbServer] stoping adb server...");
            string result = CMDUtil.ExecCMD(AdbPath, "kill-server");
            Console.WriteLine("[CMDUtil][StopAdbServer] adb server result: " + result);
        }
    }
}
