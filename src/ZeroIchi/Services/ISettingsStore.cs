using ZeroIchi.Models;

namespace ZeroIchi.Services;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
