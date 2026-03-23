using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using iMobileDevice;
using iMobileDevice.Afc;
using iMobileDevice.iDevice;

class Program
{
    static int totalFiles = 0;
    static int processedFiles = 0;

    static async Task Main(string[] args)
    {
        NativeLibraries.Load();

        // =========================
        // 保存先指定（引数 or 入力）
        // =========================
        string localRoot = args.Length > 0 ? args[0] : AskPath();

        if (string.IsNullOrWhiteSpace(localRoot))
        {
            Console.WriteLine("保存先が無効です");
            return;
        }

        Directory.CreateDirectory(localRoot);

        var udid = GetFirstDevice();
        if (udid == null)
        {
            Console.WriteLine("iPhoneが見つかりません");
            return;
        }

        Console.WriteLine($"Device: {udid}");
        Console.WriteLine($"保存先: {localRoot}");

        using var afc = CreateAfcClient(udid);

        string remoteRoot = "/DCIM";

        var allFiles = new ConcurrentBag<(string remote, string local, ulong size)>();

        Console.WriteLine("ファイル一覧取得中...");
        EnumerateFiles(afc, remoteRoot, localRoot, allFiles);

        totalFiles = allFiles.Count;
        Console.WriteLine($"対象ファイル数: {totalFiles}");

        await Parallel.ForEachAsync(allFiles, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        },
        async (item, _) =>
        {
            await CopyWithRetry(afc, item.remote, item.local, item.size);
        });

        Console.WriteLine("\n完了");
    }

    static string AskPath()
    {
        Console.Write("保存先パスを入力してください: ");
        return Console.ReadLine() ?? "";
    }

    static string GetFirstDevice()
    {
        var idevice = LibiMobileDevice.Instance.iDevice;
        idevice.idevice_get_device_list(out var list, ref int.Zero);
        return list?.FirstOrDefault();
    }

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

    static void EnumerateFiles(AfcClientHandle afc, string remotePath, string localPath,
        ConcurrentBag<(string, string, ulong)> result)
    {
        var afcApi = LibiMobileDevice.Instance.Afc;

        afcApi.afc_read_directory(afc, remotePath, out var entries);

        foreach (var entry in entries)
        {
            if (entry == "." || entry == "..") continue;

            string remote = $"{remotePath}/{entry}";
            string local = Path.Combine(localPath, entry);

            afcApi.afc_get_file_info(afc, remote, out var info);

            if (info.TryGetValue("st_ifmt", out var type) && type == "S_IFDIR")
            {
                Directory.CreateDirectory(local);
                EnumerateFiles(afc, remote, local, result);
            }
            else
            {
                ulong size = info.ContainsKey("st_size") ? ulong.Parse(info["st_size"]) : 0;
                result.Add((remote, local, size));
            }
        }
    }

    static async Task CopyWithRetry(AfcClientHandle afc, string remote, string local, ulong size)
    {
        try
        {
            if (File.Exists(local))
            {
                var fi = new FileInfo(local);
                if ((ulong)fi.Length == size)
                {
                    Interlocked.Increment(ref processedFiles);
                    PrintProgress();
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
                    await Task.Delay(500);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nエラー: {remote} - {ex.Message}");
        }
        finally
        {
            Interlocked.Increment(ref processedFiles);
            PrintProgress();
        }
    }

    static async Task CopyFile(AfcClientHandle afc, string remote, string local)
    {
        var afcApi = LibiMobileDevice.Instance.Afc;

        afcApi.afc_file_open(afc, remote, AfcFileMode.ReadOnly, out var handle);

        using var fs = new FileStream(local, FileMode.Create, FileAccess.Write);

        byte[] buffer = new byte[1024 * 256];

        while (true)
        {
            afcApi.afc_file_read(afc, handle, buffer, (uint)buffer.Length, out var read);

            if (read == 0) break;

            await fs.WriteAsync(buffer, 0, (int)read);
        }

        afcApi.afc_file_close(afc, handle);
    }

    static void PrintProgress()
    {
        int current = Interlocked.CompareExchange(ref processedFiles, 0, 0);
        Console.Write($"\r{current}/{totalFiles} ({(current * 100 / totalFiles)}%)");
    }
}
