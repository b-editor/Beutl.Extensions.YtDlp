using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.ViewModels;

public sealed record SearchProviderItem(SearchProvider Provider, string Name)
{
    public static SearchProviderItem Create(SearchProvider provider)
    {
        string? name = typeof(SearchProvider)
            .GetField(provider.ToString())
            ?.GetCustomAttribute<DisplayAttribute>()
            ?.GetName();
        return new SearchProviderItem(provider, name ?? provider.ToString());
    }
}
