using System.Collections.ObjectModel;

namespace CollectionManager.Models;

public class CollectionModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ObservableCollection<CollectionItem> Items { get; set; } = [];
    public ObservableCollection<CollectionFieldDefinition> CustomFields { get; set; } = [];

    public override string ToString() => Name;
}
