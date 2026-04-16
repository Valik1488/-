using Microsoft.JSInterop;
using MusicApp.Client.Interfaces;
using System.Text.Json;

namespace MusicApp.Client.Services;

public class LocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<T> GetItemAsync<T>(string key)
    {
        var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
        
        if (json == null)
            return default!;

        try
        {
            return JsonSerializer.Deserialize<T>(json) 
                   ?? throw new JsonException($"Failed to deserialize {key} from localStorage");
        }
        catch
        {
            Console.WriteLine($"Error deserializing {key} from localStorage");
            return default!;
        }
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value));
    }

    public async Task RemoveItemAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }
}