using PortableDeviceApiLib;
using PortableDeviceTypesLib;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace WpdBackupTool
{
    public partial class MainWindow : Window
    {
        private const int MAX_RETRY = 3;

        public MainWindow()
        {
            InitializeComponent();
        }

        // --------------------------
        // UI
        // --------------------------
        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText($"{DateTime.Now:HH:mm:ss} {msg}\n");
                LogBox.ScrollToEnd();
            });
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathBox.Text = dlg.SelectedPath;
            }
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(PathBox.Text))
            {
                MessageBox.Show("保存先を指定してください");
                return;
            }

            await Task.Run(() => Backup(PathBox.Text));
        }

        // --------------------------
        // メイン処理
        // --------------------------
        private void Backup(string rootPath)
        {
            var manager = new PortableDeviceManager();
            manager.RefreshDeviceList();

            uint count = 0;
            manager.GetDevices(null, ref count);

            if (count == 0)
            {
                Log("デバイスが見つかりません");
                return;
            }

            string[] ids = new string[count];
            manager.GetDevices(ids, ref count);

            foreach (var id in ids)
            {
                Log($"接続: {id}");

                var device = new PortableDeviceClass();
                device.Open(id, null);

                var content = (IPortableDeviceContent)device.Content();

                Enumerate(content, "DEVICE", rootPath, false);

                device.Close();
            }
        }

        // --------------------------
        // 再帰列挙（DCIMのみ）
        // --------------------------
        private void Enumerate(IPortableDeviceContent content, string objectId, string path, bool isUnderDcim)
        {
            var enumObject = content.EnumObjects(0, objectId, null);

            uint fetched = 0;
            string childId;

            do
            {
                enumObject.Next(1, out childId, ref fetched);

                if (fetched > 0)
                {
                    ProcessObject(content, childId, path, isUnderDcim);
                }

            } while (fetched > 0);
        }

        private void ProcessObject(IPortableDeviceContent content, string objectId, string path, bool isUnderDcim)
        {
            var props = content.Properties();

            var keys = new PortableDeviceKeyCollection();
            keys.Add(ref PortableDevicePKeys.WPD_OBJECT_NAME);
            keys.Add(ref PortableDevicePKeys.WPD_OBJECT_CONTENT_TYPE);
            keys.Add(ref PortableDevicePKeys.WPD_OBJECT_SIZE);

            props.GetValues(objectId, keys, out IPortableDeviceValues values);

            values.GetStringValue(ref PortableDevicePKeys.WPD_OBJECT_NAME, out string name);
            values.GetGuidValue(ref PortableDevicePKeys.WPD_OBJECT_CONTENT_TYPE, out Guid type);

            // DCIM判定
            bool nowUnderDcim = isUnderDcim || name.Equals("DCIM", StringComparison.OrdinalIgnoreCase);

            // DCIM以外はスキップ
            if (!nowUnderDcim)
            {
                if (type == PortableDevicePKeys.WPD_CONTENT_TYPE_FOLDER)
                    Enumerate(content, objectId, path, false);

                return;
            }

            string fullPath = Path.Combine(path, name);

            // --------------------------
            // フォルダ
            // --------------------------
            if (type == PortableDevicePKeys.WPD_CONTENT_TYPE_FOLDER)
            {
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Log($"フォルダ: {fullPath}");
                }

                Enumerate(content, objectId, fullPath, true);
                return;
            }

            // --------------------------
            // ファイル
            // --------------------------
            values.GetUnsignedLargeIntegerValue(ref PortableDevicePKeys.WPD_OBJECT_SIZE, out ulong size);

            // 既存チェック
            if (File.Exists(fullPath))
            {
                var fi = new FileInfo(fullPath);

                if ((ulong)fi.Length == size)
                {
                    Log($"スキップ: {fullPath}");
                    return;
                }
                else
                {
                    string renamed = RenameWithSuffix(fullPath);
                    Log($"リネーム: {renamed}");
                }
            }

            CopyWithRetry(content, objectId, fullPath, size);
        }

        // --------------------------
        // リネーム
        // --------------------------
        private string RenameWithSuffix(string path)
        {
            string dir = Path.GetDirectoryName(path)!;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            int i = 2;
            string newPath;

            do
            {
                newPath = Path.Combine(dir, $"{name}_{i}{ext}");
                i++;
            }
            while (File.Exists(newPath));

            File.Move(path, newPath);
            return newPath;
        }

        // --------------------------
        // リトライコピー
        // --------------------------
        private void CopyWithRetry(IPortableDeviceContent content, string objectId, string dest, ulong expectedSize)
        {
            for (int i = 1; i <= MAX_RETRY; i++)
            {
                try
                {
                    Log($"コピー開始: {dest} (Try {i})");

                    if (File.Exists(dest))
                        File.Delete(dest);

                    if (CopyFile(content, objectId, dest))
                    {
                        var fi = new FileInfo(dest);

                        if ((ulong)fi.Length == expectedSize)
                        {
                            Log($"OK: {dest}");
                            return;
                        }
                        else
                        {
                            Log($"サイズ不一致: {fi.Length} != {expectedSize}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"エラー: {ex.Message}");
                }

                Log($"リトライ...");
            }

            Log($"失敗: {dest}");
        }

        // --------------------------
        // 実コピー
        // --------------------------
        private bool CopyFile(IPortableDeviceContent content, string objectId, string dest)
        {
            var resources = content.Transfer();

            uint optimal = 0;

            resources.GetStream(
                objectId,
                ref PortableDevicePKeys.WPD_RESOURCE_DEFAULT,
                0,
                ref optimal,
                out System.Runtime.InteropServices.ComTypes.IStream source);

            using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write);

            byte[] buffer = new byte[optimal];
            IntPtr readPtr = Marshal.AllocHGlobal(sizeof(int));

            try
            {
                while (true)
                {
                    source.Read(buffer, buffer.Length, readPtr);
                    int read = Marshal.ReadInt32(readPtr);

                    if (read == 0)
                        break;

                    fs.Write(buffer, 0, read);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(readPtr);
            }

            return true;
        }
    }
}
