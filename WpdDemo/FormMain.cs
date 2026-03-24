using PortableDeviceApiLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WpdDemo
{
    public partial class FormMain : Form
    {
        public const uint WPD_OBJECT_ID = 2;
        public const uint WPD_OBJECT_PARENT_ID = 3;
        public const uint WPD_OBJECT_NAME = 4;
        public const uint WPD_OBJECT_CONTENT_TYPE = 7;
        public const uint WPD_OBJECT_SIZE = 11;
        public const uint WPD_OBJECT_ORIGINAL_FILE_NAME = 12;
        public const uint WPD_OBJECT_DATE_CREATED = 18;
        public const uint WPD_OBJECT_DATE_MODIFIED = 19;

        public const uint WPD_FUNCTIONAL_OBJECT_CATEGORY = 2;
        public const uint WPD_CLIENT_NAME = 2;
        public const uint WPD_CLIENT_MAJOR_VERSION = 3;
        public const uint WPD_CLIENT_MINOR_VERSION = 4;
        public const uint WPD_CLIENT_REVISION = 5;
        public const uint WPD_RESOURCE_DEFAULT = 0;
        public const uint WPD_STORAGE_FREE_SPACE_IN_BYTES = 5;

        private PortableDeviceManager deviceManager;

        class WPDDevice
        {
            public string DeviceID;
            public string DeviceName;
            public PortableDeviceClass DeviceClass;


            public override string ToString()
            {
                return string.Format("WPDデバイス名:{0}\r\n", DeviceName);
            }
        }

        public enum ObjectKind { FOLDER, FILE };

        public class PortableDeviceObject
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public ObjectKind kind { get; set; }
            public ulong? Size { get; set; }
        }



        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            init();

        }

        private void init()
        {
            deviceManager = new PortableDeviceManager();

            deviceManager.RefreshDeviceList();
            uint count = 0;
            deviceManager.GetDevices(null, ref count);

            string[] devicesIDs = new string[count];
            WPDDevice[] wpdDevice = new WPDDevice[count];

            //PortableDeviceClass[] devices = new PortableDeviceClass[count];

            deviceManager.GetDevices(devicesIDs, ref count);
            for (int i = 0; i < count; i++)
            {
                wpdDevice[i] = new WPDDevice();
                wpdDevice[i].DeviceID = devicesIDs[i];
            }

            IPortableDeviceValues clientInfo = (IPortableDeviceValues)new PortableDeviceTypesLib.PortableDeviceValuesClass();

            for (int i = 0; i < count; i++)
            {
                wpdDevice[i].DeviceClass = new PortableDeviceClass();
                wpdDevice[i].DeviceClass.Open(wpdDevice[i].DeviceID, clientInfo);

                IPortableDeviceContent content;
                wpdDevice[i].DeviceClass.Content(out content);

                IPortableDeviceProperties properties;
                content.Properties(out properties);

                IPortableDeviceValues propertyValues;
                properties.GetValues("DEVICE", null, out propertyValues);

                //wpdDevice[i].DeviceClass.Close();

                string name;
                _tagpropertykey property = new _tagpropertykey();
                property.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
                property.pid = 4;
                try
                {
                    propertyValues.GetStringValue(property, out name);
                    wpdDevice[i].DeviceName = name;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    wpdDevice[i].DeviceName = "なし";
                }

                comboBox_device.Items.Add(wpdDevice[i]);
            }
        }

        private void comboBox_device_SelectedIndexChanged(object sender, EventArgs e)
        {
            DeviceChanged();
        }

        private void DeviceChanged()
        {
            WPDDevice device = (WPDDevice)comboBox_device.SelectedItem;

            IPortableDeviceContent content;
            device.DeviceClass.Content(out content);

            IPortableDeviceProperties properties;
            content.Properties(out properties);

            IEnumPortableDeviceObjectIDs objectIDs;
            string FolderID = "DEVICE";
            content.EnumObjects(0, FolderID, null, out objectIDs);

            listView1.Items.Clear();

            string objectID;
            uint fetched = 0;

            while (true)
            {
                objectIDs.Next(1, out objectID, ref fetched);
                if (fetched <= 0) break;

                PortableDeviceObject currentObject = WrapObject(properties, objectID);

                ListViewItem li = listView1.Items.Add(currentObject.Name);

                li.SubItems.Add(currentObject.kind == ObjectKind.FILE ? "ファイル" : "フォルダ");
                li.SubItems.Add(currentObject.Id);

                // サイズ表示（フォルダは空欄）
                li.SubItems.Add(currentObject.Size.HasValue ? currentObject.Size.Value.ToString() : "");
            }
        }


        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            string FolderID = listView1.Items[listView1.SelectedItems[0].Index].SubItems[2].Text;
            WPDDevice device = (WPDDevice)comboBox_device.SelectedItem;

            IPortableDeviceContent content;
            device.DeviceClass.Content(out content);

            IPortableDeviceProperties properties;
            content.Properties(out properties);

            IEnumPortableDeviceObjectIDs objectIDs;
            content.EnumObjects(0, FolderID, null, out objectIDs);

            listView1.Items.Clear();

            string objectID;
            uint fetched = 0;

            while (true)
            {
                objectIDs.Next(1, out objectID, ref fetched);
                if (fetched <= 0) break;

                PortableDeviceObject currentObject = WrapObject(properties, objectID);

                ListViewItem li = listView1.Items.Add(currentObject.Name);

                li.SubItems.Add(currentObject.kind == ObjectKind.FILE ? "ファイル" : "フォルダ");

                li.SubItems.Add(currentObject.Id);
                // サイズ表示（フォルダは空欄）
                li.SubItems.Add(currentObject.Size.HasValue ? currentObject.Size.Value.ToString() : "");
            }

        }

        public static PortableDeviceObject WrapObject(IPortableDeviceProperties properties, string objectID)
        {
            IPortableDeviceKeyCollection keys;
            properties.GetSupportedProperties(objectID, out keys);

            IPortableDeviceValues values;
            properties.GetValues(objectID, keys, out values);

            // Get the name of the object
            string name;
            _tagpropertykey property = new _tagpropertykey();
            property.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            property.pid = WPD_OBJECT_NAME;

            try
            {
                values.GetStringValue(property, out name);
            }
            catch (System.Runtime.InteropServices.COMException exc)
            {
                Console.WriteLine($"[COMException::{exc.TargetSite}({exc.HResult})]{exc.Message}{Environment.NewLine}{exc.StackTrace}");
                name = "(non name)";
            }

            // Get the original name of the object
            string OriginalName;
            property = new _tagpropertykey();
            property.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            property.pid = WPD_OBJECT_ORIGINAL_FILE_NAME;
            try
            {
                values.GetStringValue(property, out OriginalName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Exception::{ex.TargetSite}({ex.HResult})]{ex.Message}{Environment.NewLine}{ex.StackTrace}");

                OriginalName = "";
            }

            // Get the type of the object
            Guid contentType;
            property = new _tagpropertykey();
            property.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            property.pid = WPD_OBJECT_CONTENT_TYPE;
            try
            {
                values.GetGuidValue(property, out contentType);
            }
            catch (System.Runtime.InteropServices.COMException exc)
            {
                Console.WriteLine($"[COMException::{exc.TargetSite}({exc.HResult})]{exc.Message}{Environment.NewLine}{exc.StackTrace}");

                PortableDeviceObject obj = new PortableDeviceObject();
                obj.Id = null;
                obj.Name = name == "(non name)" ? string.IsNullOrEmpty(OriginalName) ? name : OriginalName : name;
                obj.kind = ObjectKind.FOLDER;
                return obj;
            }

            Guid folderType = new Guid(0x27E2E392, 0xA111, 0x48E0, 0xAB, 0x0C, 0xE1, 0x77, 0x05, 0xA0, 0x5F, 0x85);
            Guid functionalType = new Guid(0x99ED0160, 0x17FF, 0x4C44, 0x9D, 0x98, 0x1D, 0x7A, 0x6F, 0x94, 0x19, 0x21);

            if (contentType == folderType || contentType == functionalType)
            {
                PortableDeviceObject fobj = new PortableDeviceObject();
                fobj.Id = objectID;
                fobj.Name = name == "(non name)" ? string.IsNullOrEmpty(OriginalName) ? name : OriginalName : name;
                fobj.kind = ObjectKind.FOLDER;
                return fobj;
            }

            if (OriginalName.CompareTo("") != 0)
            {
                name = OriginalName;
            }

            // Get the size of the object
            ulong size = 0;
            var sizeKey = new _tagpropertykey();
            sizeKey.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            sizeKey.pid = WPD_OBJECT_SIZE;

            bool hasSize = true;
            try
            {
                values.GetUnsignedLargeIntegerValue(sizeKey, out size);
            }
            catch
            {
                hasSize = false;
            }

            PortableDeviceObject robj = new PortableDeviceObject();
            robj.Id = objectID;
            robj.Name = name;
            robj.kind = ObjectKind.FILE;
            robj.Size = hasSize ? size : (ulong?)null;
            return robj;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;

            string localRoot = textBox_downloadRoot.Text;
            if (string.IsNullOrWhiteSpace(localRoot))
            {
                MessageBox.Show("保存先フォルダを指定してください。");
                return;
            }

            // ListView: 0=Name, 1=種別, 2=Id, 3=Size
            string id = listView1.SelectedItems[0].SubItems[2].Text;

            WPDDevice device = (WPDDevice)comboBox_device.SelectedItem;
            device.DeviceClass.Content(out IPortableDeviceContent content);
            content.Properties(out IPortableDeviceProperties properties);

            var obj = WrapObject(properties, id);

            // デバイス側フォルダ名をルート直下のフォルダとして作る（任意）
            string topName = string.IsNullOrWhiteSpace(obj.Name) ? "Download" : obj.Name;
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                topName = topName.Replace(c, '_');

            string localTop = System.IO.Path.Combine(localRoot, topName);

            if (obj.kind == ObjectKind.FOLDER)
            {
                DownloadFolderRecursive(obj.Id, localTop);
                MessageBox.Show("フォルダのダウンロードが完了しました。");
            }
            else
            {
                Directory.CreateDirectory(localRoot);
                string savePath = EnsureUniquePath(System.IO.Path.Combine(localRoot, obj.Name));
                DownloadWithRetry(obj.Id, savePath, obj.Size, maxRetry: 3);
                MessageBox.Show("ファイルのダウンロードが完了しました。");
            }
        }

        public void Download(string FileID, string FilePath)
        {
            WPDDevice device = (WPDDevice)comboBox_device.SelectedItem;

            IPortableDeviceContent content;
            device.DeviceClass.Content(out content);

            IPortableDeviceProperties properties;
            content.Properties(out properties);

            PortableDeviceObject downloadFileObj = WrapObject(properties, FileID);

            IPortableDeviceResources resources;
            content.Transfer(out resources);

            PortableDeviceApiLib.IStream wpdStream;
            uint optimalTransferSize = 0;

            var property = new _tagpropertykey();
            property.fmtid = new Guid(0xE81E79BE, 0x34F0, 0x41BF, 0xB5, 0x3F, 0xF1, 0xA0, 0x6A, 0xE8, 0x78, 0x42);
            property.pid = WPD_RESOURCE_DEFAULT;

            resources.GetStream(FileID, ref property, 0, ref optimalTransferSize, out wpdStream);
            System.Runtime.InteropServices.ComTypes.IStream sourceStream = (System.Runtime.InteropServices.ComTypes.IStream)wpdStream;

            System.IO.FileStream targetStream = new System.IO.FileStream(FilePath, System.IO.FileMode.Create, System.IO.FileAccess.Write);

            int BUFFER_SIZE = 32767;
            byte[] buffer = new byte[BUFFER_SIZE];

            IntPtr pbytesRead = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(int)));

            while (true)
            {
                sourceStream.Read(buffer, BUFFER_SIZE, pbytesRead);

                int bytesRead = Marshal.ReadInt32(pbytesRead);
                if (bytesRead <= 0)
                {
                    break;
                }
                targetStream.Write(buffer, 0, bytesRead);
            }
            targetStream.Close();

            Marshal.ReleaseComObject(sourceStream);
            Marshal.ReleaseComObject(wpdStream);
        }

        private List<PortableDeviceObject> EnumChildren(IPortableDeviceContent content, IPortableDeviceProperties properties, string parentId)
        {
            content.EnumObjects(0, parentId, null, out IEnumPortableDeviceObjectIDs enumIds);

            var list = new List<PortableDeviceObject>();
            uint fetched = 0;
            while (true)
            {
                enumIds.Next(1, out string objectId, ref fetched);
                if (fetched == 0) break;
                list.Add(WrapObject(properties, objectId));
            }
            return list;
        }
        private void DownloadFolderRecursive(string mtpFolderId, string localFolderPath)
        {
            WPDDevice device = (WPDDevice)comboBox_device.SelectedItem;

            device.DeviceClass.Content(out IPortableDeviceContent content);
            content.Properties(out IPortableDeviceProperties properties);

            Directory.CreateDirectory(localFolderPath);

            foreach (var child in EnumChildren(content, properties, mtpFolderId))
            {
                if (child.kind == ObjectKind.FOLDER)
                {
                    var folderName = SanitizeFileName(child.Name);
                    string nextLocal = Path.Combine(localFolderPath, folderName);
                    DownloadFolderRecursive(child.Id, nextLocal);
                }
                else
                {
                    var fileName = SanitizeFileName(child.Name);
                    string intendedPath = Path.Combine(localFolderPath, fileName);

                    // 差分バックアップ判定（同名同サイズならスキップ／サイズ違いなら _1）
                    var decision = DecideTargetPathWithDiffBackup(intendedPath, child.Size);
                    if (decision.Kind == BackupDecisionKind.Skip)
                        continue;

                    // 3回リトライ付き
                    DownloadWithRetry(child.Id, decision.TargetPath, child.Size, maxRetry: 3);
                }
            }
        }

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "(no name)";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private string EnsureUniquePath(string path)
        {
            if (!System.IO.File.Exists(path)) return path;

            string dir = System.IO.Path.GetDirectoryName(path);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string ext = System.IO.Path.GetExtension(path);

            for (int i = 1; ; i++)
            {
                string p = System.IO.Path.Combine(dir, $"{name} ({i}){ext}");
                if (!System.IO.File.Exists(p)) return p;
            }
        }









        private IPortableDeviceValues GetRequiredPropertiesForContentType(string FileName, string parentObjectId)
        {
            IPortableDeviceValues values = new PortableDeviceTypesLib.PortableDeviceValues() as IPortableDeviceValues;

            _tagpropertykey WPD_OBJECT_PARENT_ID_PK = new _tagpropertykey();
            WPD_OBJECT_PARENT_ID_PK.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_PARENT_ID_PK.pid = WPD_OBJECT_PARENT_ID;
            values.SetStringValue(ref WPD_OBJECT_PARENT_ID_PK, parentObjectId);

            System.IO.FileInfo fileInfo = new System.IO.FileInfo(FileName);
            var WPD_OBJECT_SIZE_PK = new _tagpropertykey();
            WPD_OBJECT_SIZE_PK.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_SIZE_PK.pid = WPD_OBJECT_SIZE;
            values.SetUnsignedLargeIntegerValue(WPD_OBJECT_SIZE_PK, (ulong)fileInfo.Length);

            var WPD_OBJECT_ORIGINAL_FILE_NAME_PK = new _tagpropertykey();
            WPD_OBJECT_ORIGINAL_FILE_NAME_PK.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_ORIGINAL_FILE_NAME_PK.pid = WPD_OBJECT_ORIGINAL_FILE_NAME;
            values.SetStringValue(WPD_OBJECT_ORIGINAL_FILE_NAME_PK, System.IO.Path.GetFileName(FileName));

            var WPD_OBJECT_NAME_PK = new _tagpropertykey();
            WPD_OBJECT_NAME_PK.fmtid = new Guid(0xEF6B490D, 0x5CD8, 0x437A, 0xAF, 0xFC, 0xDA, 0x8B, 0x60, 0xEE, 0x4A, 0x3C);
            WPD_OBJECT_NAME_PK.pid = WPD_OBJECT_NAME;
            values.SetStringValue(WPD_OBJECT_NAME_PK, System.IO.Path.GetFileName(FileName));

            return values;
        }

        private void Btn_SelectDownloadRootFolder_Click(object sender, EventArgs e)
        {
            SelectDownloadRootFolder();
        }
        private void SelectDownloadRootFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "ダウンロード先フォルダを選択してください";
                dlg.ShowNewFolderButton = true;

                // 初期フォルダ（TextBoxに入っていればそれ）
                var current = textBox_downloadRoot.Text;
                if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                {
#if NET6_0_OR_GREATER
            dlg.InitialDirectory = current;
#else
                    dlg.SelectedPath = current; // .NET Framework向け
#endif
                }

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    textBox_downloadRoot.Text = dlg.SelectedPath;
                }
            }
        }


        private enum BackupDecisionKind { Skip, Download }
        private sealed class BackupDecision
        {
            public BackupDecisionKind Kind;
            public string TargetPath;   // Download時の保存先
        }

        private BackupDecision DecideTargetPathWithDiffBackup(string intendedPath, ulong? remoteSize)
        {
            // 取得できない場合は常にダウンロード（重複回避のため連番）
            bool hasRemoteSize = remoteSize.HasValue;

            string dir = Path.GetDirectoryName(intendedPath);
            string name = Path.GetFileNameWithoutExtension(intendedPath);
            string ext = Path.GetExtension(intendedPath);

            Directory.CreateDirectory(dir);

            // まず intendedPath を確認
            if (!File.Exists(intendedPath))
                return new BackupDecision { Kind = BackupDecisionKind.Download, TargetPath = intendedPath };

            if (hasRemoteSize)
            {
                long localSize = new FileInfo(intendedPath).Length;
                if ((ulong)localSize == remoteSize.Value)
                    return new BackupDecision { Kind = BackupDecisionKind.Skip, TargetPath = intendedPath };
            }

            // 同名だがサイズ違い（またはサイズ不明）→ _1, _2...
            for (int i = 1; i < 1000000; i++)
            {
                string candidate = Path.Combine(dir, $"{name}_{i}{ext}");
                if (!File.Exists(candidate))
                    return new BackupDecision { Kind = BackupDecisionKind.Download, TargetPath = candidate };

                if (hasRemoteSize)
                {
                    long s = new FileInfo(candidate).Length;
                    if ((ulong)s == remoteSize.Value)
                        return new BackupDecision { Kind = BackupDecisionKind.Skip, TargetPath = candidate };
                }
            }

            throw new IOException("ユニークファイル名の採番上限に達しました。");
        }
        private void DownloadWithRetry(string fileId, string filePath, ulong? expectedSize, int maxRetry = 3)
{
    Exception last = null;

    for (int attempt = 1; attempt <= maxRetry; attempt++)
    {
        try
        {
            Download(fileId, filePath);

            // サイズチェック（取得できる場合のみ）
            if (expectedSize.HasValue)
            {
                var fi = new FileInfo(filePath);
                ulong actual = (ulong)fi.Length;

                if (actual != expectedSize.Value)
                {
                    throw new IOException($"サイズ不一致: expected={expectedSize.Value}, actual={actual}");
                }
            }

            // 成功
            return;
        }
        catch (Exception ex)
        {
            last = ex;

            // 中途半端なファイル削除
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }

            if (attempt == maxRetry) break;

            System.Threading.Thread.Sleep(500 * attempt); // 少し長めに
        }
    }

    throw new IOException($"ダウンロード失敗（サイズ不一致含む）: {filePath}", last);
}
    }
}
