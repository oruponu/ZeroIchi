using System.Threading.Tasks;

namespace ZeroIchi.Services;

public enum SaveChangesResult { Save, Discard, Cancel }

public interface IDialogService
{
    Task<string?> PickOpenFileAsync(string title);
    Task<string?> PickSaveFileAsync(string title, string? suggestedFileName);
    Task<SaveChangesResult> ShowSaveChangesDialogAsync();
}
