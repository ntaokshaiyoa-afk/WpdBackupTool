using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using iMobileDevice;
using iMobileDevice.Afc;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;

class Program
{
    static int totalFiles = 0;
    static int processedFiles = 0;

    static async Task Main(string[] args)
    {
        NativeLibraries.Load();

        string localRoot = args.Length > 0 ? args[0] : AskPath();
        Directory.CreateDirectory(localRoot);

        string? udid = GetFirstDevice();
        if (udid == null)
        {
            Console.WriteLine("iPhoneが見つかりません");
            return;
        }

        Console.WriteLine($"Device: {udid}");

        var afc = CreateAfcClient(udid);

        var files = new ConcurrentBag<(string remote, string local, ulong size)>();

        EnumerateFiles(afc, "/DCIM", localRoot, files);

        totalFiles = files.Count;

        Console.WriteLine($"対象: {totalFiles}");

        await Parallel.ForEachAsync(files, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, async (f, _) =>
        {
            await CopyWithRetry(afc, f.remote, f.local, f.size);
        });

        Console.WriteLine("\n完了");
    }

    static string AskPath()
    {
        Console.Write("保存先: ");
        return Console.ReadLine() ?? "";
    }

    // =========================
    // デバイス取得
    // =========================
    static string? GetFirstDevice()
    {
        var api = LibiMobileDevice.Instance.iDevice;

        IntPtr listPtr;
        int count = 0;

        api.idevice_get_device_list(out listPtr, ref count);

        if (count == 0) return null;

        IntPtr firstPtr = Marshal.ReadIntPtr(listPtr);
        string udid = Marshal.PtrToStringAnsi(firstPtr)!;

        api.idevice_device_list_free(listPtr);

        return udid;
    }

    // =========================
    // AFC接続
    // =========================
    static AfcClientHandle CreateAfcClient(string udid)
    {
        var idevice = LibiMobileDevice.Instance.iDevice;
        var lockdown = LibiMobileDevice.Instance.Lockdown;
        var afc = LibiMobileDevice.Instance.Afc;

        idevice.idevice_new(out var device, udid);

        lockdown.lockdownd_client_new_with_handshake(device, out var lockdownClient, "backup");

        lockdown.lockdownd_start_service(lockdownClient, "com.apple.afc", out var service);

        afc.afc_client_new(device, service, out var afcClient);

        return afcClient;
    }

    // =========================
    // ディレクトリ列挙
    // =========================
    static void EnumerateFiles(AfcClientHandle afc, string remote, string local,
        ConcurrentBag<(string, string, ulong)> result)
    {
        var api = LibiMobileDevice.Instance.Afc;

        IntPtr listPtr;

        api.afc_read_directory(afc, remote, out listPtr);

        int index = 0;

        while (true)
        {
            IntPtr p = Marshal.ReadIntPtr(listPtr, index * IntPtr.Size);
            if (p == IntPtr.Zero) break;

            string name = Marshal.PtrToStringAnsi(p)!;

            if (name == "." || name == "..")
            {
                index++;
                continue;
            }

            string remotePath = remote + "/" + name;
            string localPath = Path.Combine(local, name);

            var info = GetFileInfo(afc, remotePath);

            if (info.isDir)
            {
                Directory.CreateDirectory(localPath);
                EnumerateFiles(afc, remotePath, localPath, result);
            }
            else
            {
                result.Add((remotePath, localPath, info.size));
            }

            index++;
        }

        api.afc_dictionary_free(listPtr);
    }

    // =========================
    // ファイル情報取得
    // =========================
    static (bool isDir, ulong size) GetFileInfo(AfcClientHandle afc, string path)
    {
        var api = LibiMobileDevice.Instance.Afc;

        IntPtr dictPtr;
        api.afc_get_file_info(afc, path, out dictPtr);

        bool isDir = false;
        ulong size = 0;

        int i = 0;
        while (true)
        {
            IntPtr keyPtr = Marshal.ReadIntPtr(dictPtr, i * 2 * IntPtr.Size);
            if (keyPtr == IntPtr.Zero) break;

            IntPtr valPtr = Marshal.ReadIntPtr(dictPtr, (i * 2 + 1) * IntPtr.Size);

            string key = Marshal.PtrToStringAnsi(keyPtr)!;
            string val = Marshal.PtrToStringAnsi(valPtr)!;

            if (key == "st_ifmt" && val == "S_IFDIR") isDir = true;
            if (key == "st_size") ulong.TryParse(val, out size);

            i++;
        }

        api.afc_dictionary_free(dictPtr);

        return (isDir, size);
    }

    // =========================
    // コピー
    // =========================
    static async Task CopyWithRetry(AfcClientHandle afc, string remote, string local, ulong size)
    {
        try
        {
            if (File.Exists(local))
            {
                var fi = new FileInfo(local);
                if ((ulong)fi.Length == size)
                {
                    Done();
                    return;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(local)!);

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await CopyFile(afc, remote, local);
                    break;
                }
                catch
                {
                    if (i == 2) throw;
                    await Task.Delay(300);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERR: {remote} {ex.Message}");
        }
        finally
        {
            Done();
        }
    }

    static async Task CopyFile(AfcClientHandle afc, string remote, string local)
    {
        var api = LibiMobileDevice.Instance.Afc;

        api.afc_file_open(afc, remote, 1 /* READ */, out var handle);

        using var fs = new FileStream(local, FileMode.Create, FileAccess.Write);

        byte[] buffer = new byte[256 * 1024];

        while (true)
        {
            uint read = 0;

            IntPtr bufPtr = Marshal.AllocHGlobal(buffer.Length);

            api.afc_file_read(afc, handle, bufPtr, (uint)buffer.Length, ref read);

            if (read == 0)
            {
                Marshal.FreeHGlobal(bufPtr);
                break;
            }

            Marshal.Copy(bufPtr, buffer, 0, (int)read);
            await fs.WriteAsync(buffer, 0, (int)read);

            Marshal.FreeHGlobal(bufPtr);
        }

        api.afc_file_close(afc, handle);
    }

    static void Done()
    {
        Interlocked.Increment(ref processedFiles);
        int c = processedFiles;
        Console.Write($"\r{c}/{totalFiles} ({c * 100 / totalFiles}%)");
    }
}
