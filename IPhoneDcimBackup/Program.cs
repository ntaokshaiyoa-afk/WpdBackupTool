using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

        using var afc = CreateAfcClient(udid);

        var files = new ConcurrentBag<(string remote, string local, ulong size)>();

        Console.WriteLine("列挙中...");
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
    // デバイス取得（正しい書き方）
    // =========================
    static string? GetFirstDevice()
    {
        var idevice = LibiMobileDevice.Instance.iDevice;

        ReadOnlyCollection<string> udids;
        int count = 0;

        var ret = idevice.idevice_get_device_list(out udids, ref count);

        if (count == 0) return null;

        ret.ThrowOnError();

        return udids.FirstOrDefault();
    }

    // =========================
    // AFC接続
    // =========================
    static AfcClientHandle CreateAfcClient(string udid)
    {
        var idevice = LibiMobileDevice.Instance.iDevice;
        var lockdown = LibiMobileDevice.Instance.Lockdown;
        var afcApi = LibiMobileDevice.Instance.Afc;

        idevice.idevice_new(out var device, udid).ThrowOnError();

        lockdown.lockdownd_client_new_with_handshake(device, out var lockdownClient, "backup").ThrowOnError();

        lockdown.lockdownd_start_service(lockdownClient, "com.apple.afc", out var service).ThrowOnError();

        afcApi.afc_client_new(device, service, out var afc).ThrowOnError();

        return afc;
    }

    // =========================
    // 列挙
    // =========================
    static void EnumerateFiles(AfcClientHandle afc, string remote, string local,
        ConcurrentBag<(string, string, ulong)> result)
    {
        var afcApi = LibiMobileDevice.Instance.Afc;

        afcApi.afc_read_directory(afc, remote, out ReadOnlyCollection<string> entries).ThrowOnError();

        foreach (var name in entries)
        {
            if (name == "." || name == "..") continue;

            string remotePath = $"{remote}/{name}";
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
        }
    }

    // =========================
    // ファイル情報
    // =========================
    static (bool isDir, ulong size) GetFileInfo(AfcClientHandle afc, string path)
    {
        var afcApi = LibiMobileDevice.Instance.Afc;

        afcApi.afc_get_file_info(afc, path, out ReadOnlyDictionary<string, string> dict).ThrowOnError();

        bool isDir = dict.TryGetValue("st_ifmt", out var type) && type == "S_IFDIR";
        ulong size = dict.TryGetValue("st_size", out var s) ? ulong.Parse(s) : 0;

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
        var afcApi = LibiMobileDevice.Instance.Afc;

        afcApi.afc_file_open(afc, remote, AfcFileMode.Read, out var handle).ThrowOnError();

        using var fs = new FileStream(local, FileMode.Create, FileAccess.Write);

        byte[] buffer = new byte[256 * 1024];

        while (true)
        {
            uint read = 0;

            afcApi.afc_file_read(afc, handle, buffer, (uint)buffer.Length, ref read).ThrowOnError();

            if (read == 0) break;

            await fs.WriteAsync(buffer, 0, (int)read);
        }

        afcApi.afc_file_close(afc, handle);
    }

    static void Done()
    {
        int c = Interlocked.Increment(ref processedFiles);
        Console.Write($"\r{c}/{totalFiles} ({c * 100 / totalFiles}%)");
    }
}
