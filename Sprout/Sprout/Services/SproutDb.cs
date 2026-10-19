using Microsoft.JSInterop;

namespace Sprout.Services;

public class SproutDb(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/sprout-db.js");
        return _module;
    }

    public async Task<List<T>> GetAllAsync<T>(string storeName)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<List<T>>("getAll", storeName);
    }

    public async Task PutAsync<T>(string storeName, T item)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("put", storeName, item);
    }

    public async Task<bool> RequestPersistAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("requestPersist");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    public async Task<T?> GetAsync<T>(string storeName, string key)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<T?>("get", storeName, key);
    }

    public async Task ReplaceAllAsync<T>(string storeName, IEnumerable<T> items)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("replaceAll", storeName, items);
    }

    public async Task DownloadTextAsync(string fileName, string text)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("downloadText", fileName, text);
    }

    public async Task<bool> IsFolderBackupSupportedAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isFolderBackupSupported");
    }

    public async Task<string?> ChooseBackupFolderAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("chooseBackupFolder");
    }

    public async Task<string> WriteBackupFileAsync(string fileName, string text, bool allowPrompt)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("writeBackupFile", fileName, text, allowPrompt);
    }

    public async Task DeleteAsync(string storeName, object key)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("remove", storeName, key);
    }

    public async Task PutManyAsync<T>(string storeName, IEnumerable<T> items)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("putMany", storeName, items);
    }

    // ---- Weekly report ----

    public async Task<string> CopyReportAsync(string html, string text)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("copyReport", html, text);
    }

    public async Task<bool> IsFilePickerSupportedAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isFilePickerSupported");
    }

    public async Task<string?> ChooseReportWorkbookAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("chooseReportWorkbook");
    }

    public async Task<string> ReportWorkbookStatusAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("reportWorkbookStatus");
    }

    public async Task<byte[]> ReadReportWorkbookAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<byte[]>("readReportWorkbook");
    }

    public async Task<string> WriteReportWorkbookAsync(byte[] bytes)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("writeReportWorkbook", bytes);
    }

    public async Task DownloadBytesAsync(string fileName, byte[] bytes)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("downloadBytes", fileName, bytes);
    }
}
